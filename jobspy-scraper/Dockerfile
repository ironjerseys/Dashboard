FROM python:3.11-slim

WORKDIR /app

# gcc + freetds for pymssql, curl for supercronic
RUN apt-get update && \
    apt-get install -y --no-install-recommends gcc python3-dev freetds-dev freetds-bin curl && \
    rm -rf /var/lib/apt/lists/*

# supercronic : cron adapté aux containers Docker
RUN curl -fsSL "https://github.com/aptible/supercronic/releases/download/v0.2.29/supercronic-linux-amd64" \
    -o /usr/local/bin/supercronic && chmod +x /usr/local/bin/supercronic

# Dépendances Python
COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

# Code source
COPY scrape_jobs.py .
COPY jobspy/ ./jobspy/

COPY crontab /app/crontab

CMD ["supercronic", "/app/crontab"]
