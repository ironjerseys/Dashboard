#!/bin/sh
# Serveur de declenchement manuel en arriere-plan + cron planifie au premier plan.
python /app/trigger_server.py &
exec supercronic /app/crontab
