"""Mini serveur HTTP (reseau Docker interne) pour lancer le scraping a la demande depuis le Dashboard.

POST /run?job=<nom>    -> lance le script du job en arriere-plan (409 si ce job tourne deja)
GET  /status?job=<nom> -> etat du dernier run de ce job

Jobs : "jobs" (offres tech, par defaut) et "mechanics" (apprenti mecanicien au Quebec).

Si SCRAPER_TRIGGER_TOKEN est defini, l'en-tete "X-Trigger-Token" doit correspondre.
"""
import json
import os
import subprocess
import sys
import threading
from datetime import datetime, timezone
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from urllib.parse import parse_qs, urlsplit

PORT = int(os.environ.get("SCRAPER_TRIGGER_PORT", "8000"))
TOKEN = os.environ.get("SCRAPER_TRIGGER_TOKEN", "")
SCRIPTS = {
    "jobs": "/app/scrape_jobs.py",
    "mechanics": "/app/scrape_mechanics.py",
}

_lock = threading.Lock()
_states = {
    job: {"running": False, "startedAt": None, "finishedAt": None, "exitCode": None}
    for job in SCRIPTS
}


def _now():
    return datetime.now(timezone.utc).isoformat()


def _run(job):
    # La sortie va dans les logs du container, comme le run planifie.
    proc = subprocess.run([sys.executable, SCRIPTS[job]], stdout=sys.stdout, stderr=sys.stderr)
    with _lock:
        _states[job].update(running=False, finishedAt=_now(), exitCode=proc.returncode)


class Handler(BaseHTTPRequestHandler):
    def _send(self, code, body):
        payload = json.dumps(body).encode()
        self.send_response(code)
        self.send_header("Content-Type", "application/json")
        self.send_header("Content-Length", str(len(payload)))
        self.end_headers()
        self.wfile.write(payload)

    def _authorized(self):
        return not TOKEN or self.headers.get("X-Trigger-Token") == TOKEN

    def _route(self, expected_path):
        """Renvoie le job demande, ou None apres avoir repondu l'erreur."""
        url = urlsplit(self.path)
        job = parse_qs(url.query).get("job", ["jobs"])[0]
        if url.path != expected_path or job not in SCRIPTS:
            self._send(404, {"error": "not found"})
            return None
        if not self._authorized():
            self._send(401, {"error": "unauthorized"})
            return None
        return job

    def do_GET(self):
        job = self._route("/status")
        if job is None:
            return
        with _lock:
            self._send(200, dict(_states[job]))

    def do_POST(self):
        job = self._route("/run")
        if job is None:
            return
        with _lock:
            state = _states[job]
            if state["running"]:
                return self._send(409, dict(state))
            state.update(running=True, startedAt=_now(), finishedAt=None, exitCode=None)
            threading.Thread(target=_run, args=(job,), daemon=True).start()
            self._send(202, dict(state))

    def log_message(self, fmt, *args):
        print(f"[trigger] {fmt % args}", flush=True)


if __name__ == "__main__":
    print(f"[trigger] listening on :{PORT}", flush=True)
    ThreadingHTTPServer(("0.0.0.0", PORT), Handler).serve_forever()
