"""Offres d'apprenti mecanicien au Quebec, toutes villes confondues.

Sources :
  - Guichet-Emplois (guichetemplois.gc.ca) : pages de resultats HTML, filtre province QC
  - Quebec emploi (quebecemploi.gouv.qc.ca) : API JSON utilisee par le site lui-meme
  - Indeed (via jobspy), localisation Quebec

Les offres vont dans la table MechanicJobPostings : insertion des nouvelles,
mise a jour de LastSeenAt pour celles deja connues (permet de savoir si une offre est encore en ligne).
"""
import os
import re
import sys
import time
from datetime import datetime, timezone

import pandas as pd
import requests
from bs4 import BeautifulSoup

from jobspy import scrape_jobs

SEARCH_TERMS = ["apprenti mécanicien", "aide-mécanicien"]
GUICHET_MAX_PAGES = 40           # 25 offres par page
INDEED_RESULTS = 300
INDEED_HOURS_OLD = 24 * 45
PAUSE = 2                        # secondes entre deux requetes vers un meme site

USER_AGENT = (
    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 "
    "(KHTML, like Gecko) Chrome/130.0 Safari/537.36"
)

FR_MONTHS = {
    "janvier": 1, "février": 2, "fevrier": 2, "mars": 3, "avril": 4, "mai": 5, "juin": 6,
    "juillet": 7, "août": 8, "aout": 8, "septembre": 9, "octobre": 10, "novembre": 11,
    "décembre": 12, "decembre": 12,
}


def _clean(text):
    if text is None:
        return None
    text = re.sub(r"\s+", " ", str(text)).strip()
    return text or None


def _cut(text, max_len):
    text = _clean(text)
    return text[:max_len] if text else None


def _parse_fr_date(text):
    m = re.search(r"(\d{1,2})\s+([a-zéû]+)\s+(\d{4})", (text or "").lower())
    if not m or m.group(2) not in FR_MONTHS:
        return None
    return datetime(int(m.group(3)), FR_MONTHS[m.group(2)], int(m.group(1)))


def _job(source, external_id, url, title, company, city, date_posted=None,
         expires_at=None, salary=None, term=None):
    return {
        "source": source,
        "external_id": _cut(external_id, 64),
        "url": _cut(url, 800),
        "title": _cut(title, 512) or "(sans titre)",
        "company": _cut(company, 256),
        "city": _cut(city, 128),
        "date_posted": date_posted,
        "expires_at": expires_at,
        "salary": _cut(salary, 128),
        "search_term": _cut(term, 128),
    }


# ---------------------------------------------------------------- Guichet-Emplois

GUICHET_BASE = "https://www.guichetemplois.gc.ca"


def scrape_guichet(term):
    session = requests.Session()
    session.headers["User-Agent"] = USER_AGENT
    jobs, seen = [], set()

    for page in range(1, GUICHET_MAX_PAGES + 1):
        resp = session.get(
            f"{GUICHET_BASE}/jobsearch/jobsearch",
            params={"searchstring": term, "fprov": "QC", "sort": "D", "page": page},
            timeout=30,
        )
        resp.raise_for_status()
        soup = BeautifulSoup(resp.text, "html.parser")
        articles = soup.select("article[id^=article-]")
        new_on_page = 0

        for art in articles:
            ext_id = art["id"].removeprefix("article-")
            if ext_id in seen:
                continue
            seen.add(ext_id)
            new_on_page += 1

            def text(selector):
                el = art.select_one(selector)
                if not el:
                    return None
                # Textes d'accessibilite et infobulles (ex. "annoncee par une agence de recrutement").
                for hidden in el.select(".wb-inv, .description"):
                    hidden.decompose()
                return _clean(el.get_text(" "))

            location = text("li.location") or ""
            salary = text("li.salary")
            jobs.append(_job(
                "guichet-emplois", ext_id,
                f"{GUICHET_BASE}/rechercheemplois/offredemploi/{ext_id}",
                text(".noctitle"), text("li.business"),
                re.sub(r"\s*\((QC|Qc)\)\s*$", "", location),
                date_posted=_parse_fr_date(text("li.date")),
                salary=re.sub(r"^Salaire\s*:?\s*", "", salary) if salary else None,
                term=term,
            ))

        # Une page vide ou deja vue = fin des resultats.
        if not articles or new_on_page == 0:
            break
        time.sleep(PAUSE)

    return jobs


# ---------------------------------------------------------------- Quebec emploi

QE_BASE = "https://www.quebecemploi.gouv.qc.ca"


def scrape_quebec_emploi(term):
    body = {
        "sort": {"type": "AUTO"}, "langue": "fr", "page": 1, "identAWS": "",
        "filter": {
            "inputSearch": term, "address": "",
            "localisation": {"longitude": "", "latitude": "", "distance": 20},
            "adminRegion": [], "offerType": [], "commitment": [], "jobDuration": [],
            "levelEducation": [], "studyDiscipline": [], "mrc": [], "bsq": [], "scian": [],
            "postedSince": "", "excludeAgencies": False, "isUkrainian": False,
            "isExperimente": False, "isSubsidized": False, "isTrainingProgram": False,
        },
    }
    # L'API renvoie tous les resultats d'un coup (le site pagine cote client).
    resp = requests.post(f"{QE_BASE}/search/postingFilteredAI", json=body,
                         headers={"User-Agent": USER_AGENT}, timeout=60)
    resp.raise_for_status()

    jobs = []
    for item in resp.json().get("items", []):
        ext_id = str(item.get("ide_affch"))
        expires = None
        if item.get("expiration_date"):
            try:
                expires = datetime.fromisoformat(item["expiration_date"][:10])
            except ValueError:
                pass
        jobs.append(_job(
            "quebec-emploi", ext_id,
            f"{QE_BASE}/plateforme-emploi/poste/{ext_id}",
            item.get("titre"), item.get("employeur"), item.get("nom_ville"),
            expires_at=expires, term=term,
        ))
    return jobs


# ---------------------------------------------------------------- Indeed

def _indeed_salary(row):
    lo, hi = row.get("min_amount"), row.get("max_amount")
    lo = None if pd.isna(lo) else lo
    hi = None if pd.isna(hi) else hi
    if lo is None and hi is None:
        return None
    interval = {"hourly": "/h", "yearly": "/an", "monthly": "/mois", "weekly": "/sem"}.get(row.get("interval"), "")
    amount = f"{lo:g} – {hi:g}" if lo is not None and hi is not None and lo != hi else f"{(lo or hi):g}"
    return f"{amount} ${interval}"


def scrape_indeed(term):
    df = scrape_jobs(
        site_name=["indeed"],
        search_term=term,
        location="Québec",
        country_indeed="Canada",
        results_wanted=INDEED_RESULTS,
        hours_old=INDEED_HOURS_OLD,
        verbose=1,
    )
    jobs = []
    for _, row in df.iterrows():
        location = _clean(row.get("location")) or ""
        # Indeed deborde parfois hors province : on ne garde que le Quebec.
        if ", QC" not in location:
            continue
        posted = row.get("date_posted")
        posted = None if posted is None or pd.isna(posted) else pd.to_datetime(posted).to_pydatetime()
        jobs.append(_job(
            "indeed", _clean(row.get("id")), row.get("job_url"),
            row.get("title"), row.get("company"), location.split(",")[0],
            date_posted=posted, salary=_indeed_salary(row), term=term,
        ))
    return jobs


# ---------------------------------------------------------------- Base de donnees

def save(jobs):
    password = os.environ.get("MSSQL_PASSWORD", "")
    if not password:
        print("MSSQL_PASSWORD non défini : rien n'est enregistré.")
        return False

    import pymssql

    conn = pymssql.connect(
        server=os.environ.get("MSSQL_SERVER", "sql"),
        port=int(os.environ.get("MSSQL_PORT", "1433")),
        user=os.environ.get("MSSQL_USER", "sa"),
        password=password,
        database=os.environ.get("MSSQL_DATABASE", "DashboardDb"),
        login_timeout=30,
    )
    cursor = conn.cursor()
    cursor.execute("SELECT Url FROM MechanicJobPostings")
    existing = {row[0] for row in cursor.fetchall()}

    now = datetime.now(timezone.utc).replace(tzinfo=None)
    new = [j for j in jobs if j["url"] not in existing]
    known = [j for j in jobs if j["url"] in existing]

    cursor.executemany(
        """
        INSERT INTO MechanicJobPostings
            (Source, ExternalId, Url, Title, Company, City, DatePosted, ExpiresAt,
             Salary, SearchTerm, FirstSeenAt, LastSeenAt)
        VALUES (%s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s)
        """,
        [(j["source"], j["external_id"], j["url"], j["title"], j["company"], j["city"],
          j["date_posted"], j["expires_at"], j["salary"], j["search_term"], now, now) for j in new],
    )
    cursor.executemany(
        "UPDATE MechanicJobPostings SET LastSeenAt = %s, ExpiresAt = COALESCE(%s, ExpiresAt) WHERE Url = %s",
        [(now, j["expires_at"], j["url"]) for j in known],
    )
    conn.commit()
    conn.close()
    print(f"✓ {len(new)} nouvelles offres, {len(known)} déjà connues mises à jour.")
    return True


SOURCES = [
    ("Guichet-Emplois", scrape_guichet),
    ("Québec emploi", scrape_quebec_emploi),
    ("Indeed", scrape_indeed),
]


def main():
    by_url = {}
    failures = 0

    for name, scraper in SOURCES:
        for term in SEARCH_TERMS:
            print(f"\n[{name}] {term}", flush=True)
            try:
                found = scraper(term)
                for job in found:
                    if job["url"]:
                        by_url.setdefault(job["url"], job)
                print(f"  -> {len(found)} offres", flush=True)
            except Exception as e:
                failures += 1
                print(f"  -> Erreur : {e}", flush=True)
            time.sleep(PAUSE)

    jobs = list(by_url.values())
    print(f"\nTOTAL : {len(jobs)} offres uniques")
    for source in sorted({j["source"] for j in jobs}):
        print(f"  {source}: {sum(j['source'] == source for j in jobs)}")

    if jobs and not save(jobs):
        return 1
    # Echec si aucune source n'a repondu.
    return 1 if failures == len(SOURCES) * len(SEARCH_TERMS) else 0


if __name__ == "__main__":
    sys.exit(main())
