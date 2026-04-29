import csv
import os
import time
from datetime import date, datetime, timezone
import pandas as pd
from jobspy import scrape_jobs

ROLES = [
    {
        "search_term": ".NET developer",
        "google_suffix": ".NET developer C# jobs near",
        "label": "dotnet",
    },
    {
        "search_term": "DevOps Kubernetes",
        "google_suffix": "DevOps Kubernetes engineer jobs near",
        "label": "devops_kubernetes",
    },
    {
        "search_term": "ML engineer",
        "google_suffix": "Machine Learning engineer jobs near",
        "label": "ml_engineer",
    },
]

LOCATIONS = [
    {"location": "Vancouver, BC",      "country_indeed": "Canada",  "city_label": "vancouver"},
    {"location": "Toronto, ON",        "country_indeed": "Canada",  "city_label": "toronto"},
    {"location": "Montreal, QC",       "country_indeed": "Canada",  "city_label": "montreal"},
    {"location": "Paris, France",      "country_indeed": "France",  "city_label": "paris"},
    {"location": "Lyon, France",       "country_indeed": "France",  "city_label": "lyon"},
    {"location": "Toulouse, France",   "country_indeed": "France",  "city_label": "toulouse"},
]

SITES = ["linkedin", "indeed", "glassdoor", "google"]
RESULTS_PER_SITE = 1000
PAUSE_BETWEEN_SEARCHES = 5  # seconds

all_jobs: list[pd.DataFrame] = []
total_searches = len(ROLES) * len(LOCATIONS)
count = 0

for loc in LOCATIONS:
    for role in ROLES:
        count += 1
        label = f"{role['label']}_{loc['city_label']}"
        print(f"\n[{count}/{total_searches}] {role['search_term']} | {loc['location']}")

        try:
            jobs = scrape_jobs(
                site_name=SITES,
                search_term=role["search_term"],
                google_search_term=f"{role['google_suffix']} {loc['location']} since last month",
                location=loc["location"],
                country_indeed=loc["country_indeed"],
                results_wanted=RESULTS_PER_SITE,
                hours_old=720,  # 30 days
                linkedin_fetch_description=True,
                verbose=1,
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
            print(f"  -> Erreur: {e}")

        if count < total_searches:
            time.sleep(PAUSE_BETWEEN_SEARCHES)

if not all_jobs:
    print("\nAucun résultat trouvé.")
    exit(0)

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
    try:
        import pymssql
    except ImportError:
        print("pymssql non disponible, passage au fallback CSV.")
        return False

    server   = os.environ.get("MSSQL_SERVER", "sql")
    port     = int(os.environ.get("MSSQL_PORT", "1433"))
    user     = os.environ.get("MSSQL_USER", "sa")
    password = os.environ.get("MSSQL_PASSWORD", "")
    database = os.environ.get("MSSQL_DATABASE", "DashboardDb")

    if not password:
        print("MSSQL_PASSWORD non défini, passage au fallback CSV.")
        return False

    try:
        conn = pymssql.connect(
            server=server, port=port,
            user=user, password=password,
            database=database,
            login_timeout=30,
        )
    except Exception as e:
        print(f"Connexion SQL Server échouée: {e}\nPassage au fallback CSV.")
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


if not insert_to_db(combined):
    # Fallback : écriture CSV (comportement original)
    output_file = f"jobs_results_{date.today().strftime('%Y-%m-%d')}.csv"
    combined.to_csv(output_file, quoting=csv.QUOTE_NONNUMERIC, escapechar="\\", index=False)
    print(f"\n✓ Fallback CSV : '{output_file}' ({len(combined)} lignes)")
