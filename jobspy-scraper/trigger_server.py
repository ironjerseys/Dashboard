"""Mini serveur HTTP (reseau Docker interne) pour lancer le scraping a la demande depuis le Dashboard.

POST /run    -> lance scrape_jobs.py en arriere-plan (409 si un run est deja en cours)
GET  /status -> etat du dernier run

Si SCRAPER_TRIGGER_TOKEN est defini, l'en-tete "X-Trigger-Token" doit correspondre.
"""
import json
import os
import subprocess
import sys
import threading
from datetime import datetime, timezone
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

PORT = int(os.environ.get("SCRAPER_TRIGGER_PORT", "8000"))
TOKEN = os.environ.get("SCRAPER_TRIGGER_TOKEN", "")
SCRIPT = "/app/scrape_jobs.py"

_lock = threading.Lock()
_state = {"running": False, "startedAt": None, "finishedAt": None, "exitCode": None}


def _now():
    return datetime.now(timezone.utc).isoformat()


def _run():
    # La sortie va dans les logs du container, comme le run planifie.
    proc = subprocess.run([sys.executable, SCRIPT], stdout=sys.stdout, stderr=sys.stderr)
    with _lock:
        _state.update(running=False, finishedAt=_now(), exitCode=proc.returncode)


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

    def do_GET(self):
        if self.path != "/status":
            return self._send(404, {"error": "not found"})
        if not self._authorized():
            return self._send(401, {"error": "unauthorized"})
        with _lock:
            self._send(200, dict(_state))

    def do_POST(self):
        if self.path != "/run":
            return self._send(404, {"error": "not found"})
        if not self._authorized():
            return self._send(401, {"error": "unauthorized"})
        with _lock:
            if _state["running"]:
                return self._send(409, dict(_state))
            _state.update(running=True, startedAt=_now(), finishedAt=None, exitCode=None)
            threading.Thread(target=_run, daemon=True).start()
            self._send(202, dict(_state))

    def log_message(self, fmt, *args):
        print(f"[trigger] {fmt % args}", flush=True)


if __name__ == "__main__":
    print(f"[trigger] listening on :{PORT}", flush=True)
    ThreadingHTTPServer(("0.0.0.0", PORT), Handler).serve_forever()
