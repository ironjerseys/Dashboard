import csv
import logging
import os
import re
import time
from datetime import date, datetime, timezone
import pandas as pd
from jobspy import scrape_jobs

# Liste de secours, utilisée seulement si la table ScraperRoles est inaccessible ou vide.
# Les postes se gèrent depuis la page /job-data (bouton Paramètres) : ils sont lus en base.
DEFAULT_ROLES = [
    {
        "search_term": ".NET developer",
        "label": "dotnet",
    },
    {
        "search_term": "DevOps Kubernetes",
        "label": "devops_kubernetes",
    },
    {
        "search_term": "ML engineer",
        "label": "ml_engineer",
    },
    {
        "search_term": "GRC Governance Risk Compliance",
        "label": "grc",
    },
    {
        "search_term": "Business analyst",
        "label": "business_analyst",
    },
    {
        "search_term": "Analyste fonctionnel",
        "label": "analyste_fonctionnel",
    },
    {
        "search_term": "Support applicatif",
        "label": "support_applicatif",
    },
]



def _connect():
    """Connexion SQL Server via les variables d'environnement, ou None si impossible."""
    try:
        import pymssql
    except ImportError:
        print("pymssql non disponible.")
        return None

    password = os.environ.get("MSSQL_PASSWORD", "")
    if not password:
        print("MSSQL_PASSWORD non défini.")
        return None

    try:
        return pymssql.connect(
            server=os.environ.get("MSSQL_SERVER", "sql"),
            port=int(os.environ.get("MSSQL_PORT", "1433")),
            user=os.environ.get("MSSQL_USER", "sa"),
            password=password,
            database=os.environ.get("MSSQL_DATABASE", "DashboardDb"),
            login_timeout=30,
        )
    except Exception as e:
        print(f"Connexion SQL Server échouée: {e}")
        return None


def _slug(text):
    return re.sub(r"[^a-z0-9]+", "_", text.lower()).strip("_") or "role"


def load_roles():
    """Postes à chercher, lus dans la table ScraperRoles (repli sur DEFAULT_ROLES)."""
    conn = _connect()
    if conn is None:
        print("Postes : liste par défaut (base inaccessible).")
        return DEFAULT_ROLES

    try:
        cursor = conn.cursor()
        cursor.execute("SELECT SearchTerm FROM ScraperRoles ORDER BY Id")
        rows = cursor.fetchall()
    except Exception as e:
        print(f"Lecture de ScraperRoles impossible: {e}")
        rows = []
    finally:
        conn.close()

    if not rows:
        print("Postes : liste par défaut (table ScraperRoles vide ou absente).")
        return DEFAULT_ROLES

    roles = [{"search_term": term, "label": _slug(term)} for (term,) in rows]
    print(f"Postes lus en base ({len(roles)}): {', '.join(r['search_term'] for r in roles)}")
    return roles


ROLES = load_roles()

LOCATIONS = [
    {"location": "Vancouver, BC",      "country_indeed": "Canada",  "city_label": "vancouver"},
    {"location": "Toronto, ON",        "country_indeed": "Canada",  "city_label": "toronto"},
    {"location": "Montreal, QC",       "country_indeed": "Canada",  "city_label": "montreal"},
    {"location": "Paris, France",      "country_indeed": "France",  "city_label": "paris"},
    {"location": "Lyon, France",       "country_indeed": "France",  "city_label": "lyon"},
    {"location": "Toulouse, France",   "country_indeed": "France",  "city_label": "toulouse"},
]

# Google et Glassdoor retirés : ils ne renvoyaient jamais d'offre.
SITES = ["linkedin", "indeed"]
RESULTS_PER_SITE = 1000
HOURS_OLD = 720  # 30 jours
PAUSE_BETWEEN_SEARCHES = 5  # seconds


# ── Détection des recherches en échec ──
# jobspy ne lève pas d'exception quand un site bloque (LinkedIn 429) ou répond en erreur :
# il écrit un message dans son logger et renvoie un résultat partiel. On capture ces messages
# pour marquer la recherche comme incomplète dans le système V2.
SITE_LOGGERS = {"linkedin": "JobSpy:LinkedIn", "indeed": "JobSpy:Indeed"}
ERROR_HINTS = ("status code", "blocked", "429", "bad proxy")


class _SiteErrorCollector(logging.Handler):
    def __init__(self, site):
        super().__init__(level=logging.INFO)
        self.site = site
        self.messages = []

    def emit(self, record):
        msg = record.getMessage()
        if record.levelno >= logging.ERROR or any(h in msg.lower() for h in ERROR_HINTS):
            self.messages.append(msg)


_collectors = {}
for _site, _logger_name in SITE_LOGGERS.items():
    _logger = logging.getLogger(_logger_name)
    # verbose=2 laisse passer les messages INFO (Indeed signale ses erreurs en INFO) ;
    # la console garde le niveau WARNING, comme avant avec verbose=1.
    for _h in _logger.handlers:
        _h.setLevel(logging.WARNING)
    _collectors[_site] = _SiteErrorCollector(_site)
    _logger.addHandler(_collectors[_site])

RUN_STARTED_UTC = datetime.now(timezone.utc).replace(tzinfo=None)

all_jobs: list[pd.DataFrame] = []
# Une entrée par recherche (poste × ville × site) pour le système V2.
searches_v2: list[dict] = []
total_searches = len(ROLES) * len(LOCATIONS)
count = 0

for loc in LOCATIONS:
    for role in ROLES:
        count += 1
        label = f"{role['label']}_{loc['city_label']}"
        print(f"\n[{count}/{total_searches}] {role['search_term']} | {loc['location']}")

        for _c in _collectors.values():
            _c.messages.clear()

        jobs = pd.DataFrame()
        search_error = None
        try:
            jobs = scrape_jobs(
                site_name=SITES,
                search_term=role["search_term"],
                location=loc["location"],
                country_indeed=loc["country_indeed"],
                results_wanted=RESULTS_PER_SITE,
                hours_old=HOURS_OLD,
                linkedin_fetch_description=True,
                verbose=2,
            )

            if not jobs.empty:
                jobs["search_label"] = label
                jobs["search_role"] = role["search_term"]
                jobs["search_city"] = loc["location"]
                all_jobs.append(jobs)
                print(f"  -> {len(jobs)} postes trouvés")
            else:
                print("  -> Aucun résultat")

        except Exception as e:
            search_error = str(e)
            print(f"  -> Erreur: {e}")

        for site in SITES:
            site_jobs = jobs[jobs["site"] == site] if not jobs.empty and "site" in jobs else pd.DataFrame()
            messages = _collectors[site].messages
            error = search_error or ("; ".join(dict.fromkeys(messages)) if messages else None)
            urls = list(dict.fromkeys(site_jobs["job_url"].dropna().astype(str))) if not site_jobs.empty else []
            searches_v2.append({
                "role": role["search_term"],
                "city": loc["location"],
                "site": site,
                "count": len(urls),
                "succeeded": error is None,
                "capped": len(urls) >= RESULTS_PER_SITE,
                "error": error[:1024] if error else None,
                "jobs": site_jobs,
            })
            if error:
                print(f"     {site}: incomplet ({error[:120]})")

        if count < total_searches:
            time.sleep(PAUSE_BETWEEN_SEARCHES)

combined = None
if all_jobs:
    combined = pd.concat(all_jobs, ignore_index=True)
    combined.drop_duplicates(subset=["job_url"], keep="first", inplace=True)

    print(f"\n{'='*70}")
    print(f"TOTAL: {len(combined)} postes uniques collectés")
    print(f"{'='*70}")

    summary = (
        combined
        .groupby(["search_role", "search_city", "site"])
        .size()
        .reset_index(name="count")
        .pivot_table(index=["search_role", "search_city"], columns="site", values="count", fill_value=0)
    )
    print("\nRésumé par rôle / ville / site:")
    print(summary.to_string())


def _safe_str(val, max_len=None):
    if val is None:
        return None
    if isinstance(val, float) and pd.isna(val):
        return None
    s = str(val).strip()
    if not s:
        return None
    return s[:max_len] if max_len else s


def _safe_decimal(val):
    if val is None:
        return None
    if isinstance(val, float) and pd.isna(val):
        return None
    try:
        return float(val)
    except Exception:
        return None


def _safe_bool(val):
    if val is None:
        return None
    if isinstance(val, float) and pd.isna(val):
        return None
    if isinstance(val, bool):
        return val
    if isinstance(val, str):
        return val.lower() in ("true", "1", "yes")
    return bool(val)


def _safe_date(val):
    if val is None:
        return None
    if isinstance(val, float) and pd.isna(val):
        return None
    if isinstance(val, datetime):
        return val
    if isinstance(val, date):
        return datetime(val.year, val.month, val.day)
    try:
        return pd.to_datetime(val).to_pydatetime()
    except Exception:
        return None


def insert_to_db(df: pd.DataFrame) -> bool:
    conn = _connect()
    if conn is None:
        print("Passage au fallback CSV.")
        return False

    cursor = conn.cursor()

    # Récupérer les URLs déjà en base pour éviter les doublons
    cursor.execute("SELECT JobUrl FROM JobPostings")
    existing_urls = {row[0] for row in cursor.fetchall()}
    print(f"\n{len(existing_urls)} offres déjà en base.")

    new_jobs = df[~df["job_url"].isin(existing_urls)]
    print(f"{len(new_jobs)} nouvelles offres à insérer.")

    if new_jobs.empty:
        conn.close()
        return True

    now = datetime.now(timezone.utc)

    rows = []
    for _, row in new_jobs.iterrows():
        rows.append((
            _safe_str(row.get("site"), 64),
            _safe_str(row.get("job_url"), 2048),
            _safe_str(row.get("job_url_direct"), 2048),
            _safe_str(row.get("title"), 512),
            _safe_str(row.get("company"), 256),
            _safe_str(row.get("location"), 256),
            _safe_date(row.get("date_posted")),
            _safe_str(row.get("job_type"), 64),
            _safe_str(row.get("interval"), 32),
            _safe_decimal(row.get("min_amount")),
            _safe_decimal(row.get("max_amount")),
            _safe_str(row.get("currency"), 16),
            _safe_bool(row.get("is_remote")),
            _safe_str(row.get("job_level"), 128),
            _safe_str(row.get("search_role"), 128),
            _safe_str(row.get("search_city"), 128),
            now,
        ))

    # Batch insert par tranches de 500 pour éviter les timeouts
    BATCH = 500
    inserted = 0
    for i in range(0, len(rows), BATCH):
        batch = rows[i:i + BATCH]
        cursor.executemany(
            """
            INSERT INTO JobPostings
                (Site, JobUrl, JobUrlDirect, Title, Company, Location,
                 DatePosted, JobType, Interval, MinAmount, MaxAmount, Currency,
                 IsRemote, JobLevel, SearchRole, SearchCity, ScrapedAt)
            VALUES (%s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s)
            """,
            batch,
        )
        conn.commit()
        inserted += len(batch)
        print(f"  {inserted}/{len(rows)} insérées…")

    conn.close()
    print(f"\n✓ {len(rows)} nouvelles offres insérées en base.")
    return True


V2_URL_MAX = 800  # même limite que la colonne JobPostingsV2.JobUrl (clé d'index)


def _chunks(items, size):
    for i in range(0, len(items), size):
        yield items[i:i + size]


def write_v2(searches: list[dict]) -> None:
    """Système V2 : enregistre la photo complète du run (toutes les offres vues, par recherche).

    Indépendant de insert_to_db : une erreur ici n'empêche pas l'ancien système d'écrire.
    Tout est écrit dans une transaction : un run est soit complet en base, soit absent.
    """
    conn = _connect()
    if conn is None:
        print("V2 : base inaccessible, run non enregistré.")
        return

    finished = datetime.now(timezone.utc).replace(tzinfo=None)
    try:
        cursor = conn.cursor()

        cursor.execute(
            """
            INSERT INTO JobScrapeRunsV2 (StartedUtc, FinishedUtc, Status, IsBackfill, HoursOld, ResultsPerSite)
            OUTPUT INSERTED.Id
            VALUES (%s, %s, 'Completed', 0, %s, %s)
            """,
            (RUN_STARTED_UTC, finished, HOURS_OLD, RESULTS_PER_SITE),
        )
        run_id = cursor.fetchone()[0]

        # 1. Offres uniques de tout le run (première occurrence de chaque URL).
        frames = [s["jobs"] for s in searches if not s["jobs"].empty]
        offers = {}
        for frame in frames:
            for _, row in frame.iterrows():
                url = _safe_str(row.get("job_url"), V2_URL_MAX)
                if url and url not in offers:
                    offers[url] = row

        cursor.execute("SELECT Id, JobUrl FROM JobPostingsV2")
        ids = {url: pid for pid, url in cursor.fetchall()}

        new_rows = [
            (
                _safe_str(row.get("site"), 64),
                url,
                _safe_str(row.get("job_url_direct"), 2048),
                _safe_str(row.get("title"), 512) or "",
                _safe_str(row.get("company"), 256),
                _safe_str(row.get("location"), 256),
                _safe_date(row.get("date_posted")),
                _safe_str(row.get("job_type"), 64),
                _safe_str(row.get("interval"), 32),
                _safe_decimal(row.get("min_amount")),
                _safe_decimal(row.get("max_amount")),
                _safe_str(row.get("currency"), 16),
                _safe_bool(row.get("is_remote")),
                _safe_str(row.get("job_level"), 128),
                RUN_STARTED_UTC,
                RUN_STARTED_UTC,
            )
            for url, row in offers.items() if url not in ids
        ]
        for batch in _chunks(new_rows, 500):
            cursor.executemany(
                """
                INSERT INTO JobPostingsV2
                    (Site, JobUrl, JobUrlDirect, Title, Company, Location, DatePosted, JobType, Interval,
                     MinAmount, MaxAmount, Currency, IsRemote, JobLevel, FirstSeenUtc, LastSeenUtc)
                VALUES (%s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s)
                """,
                batch,
            )

        if new_rows:
            cursor.execute("SELECT Id, JobUrl FROM JobPostingsV2")
            ids = {url: pid for pid, url in cursor.fetchall()}

        seen_ids = sorted({ids[url] for url in offers if url in ids})
        for batch in _chunks(seen_ids, 1000):
            cursor.execute(
                f"UPDATE JobPostingsV2 SET LastSeenUtc = %s WHERE Id IN ({','.join(str(i) for i in batch)})",
                (RUN_STARTED_UTC,),
            )

        # 2. Recherches et "offre vue par cette recherche".
        sightings = 0
        for s in searches:
            cursor.execute(
                """
                INSERT INTO JobScrapeSearchesV2 (RunId, Role, City, Site, ResultCount, Succeeded, IsCapped, Error)
                OUTPUT INSERTED.Id
                VALUES (%s, %s, %s, %s, %s, %s, %s, %s)
                """,
                (run_id, s["role"][:128], s["city"][:128], s["site"], s["count"], s["succeeded"], s["capped"], s["error"]),
            )
            search_id = cursor.fetchone()[0]

            posting_ids = set()
            if not s["jobs"].empty:
                for url in s["jobs"]["job_url"].dropna().astype(str):
                    pid = ids.get(_safe_str(url, V2_URL_MAX))
                    if pid:
                        posting_ids.add(pid)
            rows = [(search_id, pid) for pid in posting_ids]
            for batch in _chunks(rows, 1000):
                cursor.executemany("INSERT INTO JobSightingsV2 (SearchId, JobPostingId) VALUES (%s, %s)", batch)
            sightings += len(rows)

        conn.commit()
        failed = sum(1 for s in searches if not s["succeeded"])
        print(f"\n✓ V2 : run #{run_id} enregistré — {len(offers)} offres vues, {len(new_rows)} nouvelles, "
              f"{sightings} liens recherche/offre, {failed} recherche(s) incomplète(s) sur {len(searches)}.")
    except Exception as e:
        conn.rollback()
        print(f"\n✗ V2 : échec de l'enregistrement du run ({e}). L'ancien système n'est pas affecté.")
    finally:
        conn.close()


# Système V2 d'abord : il enregistre aussi un run sans aucun résultat (tout bloqué),
# ce qui se verra sur la page /job-data-v2 au lieu de passer inaperçu.
write_v2(searches_v2)

# Ancien système (page /job-data) : inchangé, n'insère que les nouvelles offres.
if combined is None:
    print("\nAucun résultat trouvé.")
elif not insert_to_db(combined):
    # Fallback : écriture CSV (comportement original)
    output_file = f"jobs_results_{date.today().strftime('%Y-%m-%d')}.csv"
    combined.to_csv(output_file, quoting=csv.QUOTE_NONNUMERIC, escapechar="\\", index=False)
    print(f"\n✓ Fallback CSV : '{output_file}' ({len(combined)} lignes)")
