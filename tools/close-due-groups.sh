#!/usr/bin/env bash
# Closes Secret Santa groups whose deadline has passed (draws gifts + sends emails).
#
# The API on Render's free plan sleeps after ~15 min of inactivity and needs about a minute to wake up,
# so the script first pings it repeatedly until it answers (including the database), then closes the groups.
#
# Config (environment variables or a file next to the script: close-due-groups.env):
#   SS_API_URL   e.g. https://secretsanta-api.onrender.com
#   SS_API_KEY   value of ApiSecurity__InternalApiKey from Render (secretsanta-api → Environment)
#
# Cron example (every hour, log to file):
#   0 * * * * /opt/secretsanta/close-due-groups.sh >> /var/log/secretsanta-close.log 2>&1

set -uo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
if [[ -f "$SCRIPT_DIR/close-due-groups.env" ]]; then
    # shellcheck source=/dev/null
    source "$SCRIPT_DIR/close-due-groups.env"
fi

: "${SS_API_URL:?Set SS_API_URL (e.g. https://secretsanta-api.onrender.com)}"
: "${SS_API_KEY:?Set SS_API_KEY (ApiSecurity__InternalApiKey from Render)}"
SS_API_URL="${SS_API_URL%/}"

WAKE_ATTEMPTS="${WAKE_ATTEMPTS:-12}"   # 12 × (up to 30 s request + 15 s pause) ≈ up to 9 min
WAKE_PAUSE="${WAKE_PAUSE:-15}"
CLOSE_ATTEMPTS="${CLOSE_ATTEMPTS:-3}"
CLOSE_PAUSE="${CLOSE_PAUSE:-30}"

log() { echo "$(date '+%Y-%m-%d %H:%M:%S') $*"; }

# Don't run twice at the same time (e.g. a slow wake-up overlapping the next cron run)
if command -v flock >/dev/null 2>&1; then
    LOCK_FILE="${TMPDIR:-/tmp}/secretsanta-close-due.lock"
    exec 9>"$LOCK_FILE"
    if ! flock -n 9; then
        log "Another run is still in progress, skipping."
        exit 0
    fi
fi

# 1) Wake the API up and wait until it can reach the database
awake=false
for ((i = 1; i <= WAKE_ATTEMPTS; i++)); do
    body="$(curl -sS --max-time 30 -H "X-Internal-Api-Key: $SS_API_KEY" -w $'\n%{http_code}' "$SS_API_URL/api/ping" 2>&1)"
    code="${body##*$'\n'}"
    body="${body%$'\n'*}"

    if [[ "$code" == "200" && "$body" == *'"database":true'* ]]; then
        log "API is up (attempt $i)."
        awake=true
        break
    fi
    if [[ "$code" == "401" ]]; then
        log "API rejected the key (401) – check SS_API_KEY."
        exit 2
    fi

    log "API not ready yet (attempt $i/$WAKE_ATTEMPTS, HTTP $code): ${body:0:200}"
    ((i < WAKE_ATTEMPTS)) && sleep "$WAKE_PAUSE"
done

if [[ "$awake" != true ]]; then
    log "API did not wake up, giving up. The next cron run will try again."
    exit 1
fi

# 2) Close the due groups. Safe to retry: already closed groups are skipped, so nobody gets an email twice.
for ((i = 1; i <= CLOSE_ATTEMPTS; i++)); do
    body="$(curl -sS --max-time 180 -X POST -H "X-Internal-Api-Key: $SS_API_KEY" -H "Content-Length: 0" \
        -w $'\n%{http_code}' "$SS_API_URL/api/group/close-due" 2>&1)"
    code="${body##*$'\n'}"
    body="${body%$'\n'*}"

    if [[ "$code" == "200" ]]; then
        log "Done: $body"
        exit 0
    fi

    log "Closing failed (attempt $i/$CLOSE_ATTEMPTS, HTTP $code): ${body:0:300}"
    ((i < CLOSE_ATTEMPTS)) && sleep "$CLOSE_PAUSE"
done

log "Closing groups failed, the next cron run will try again."
exit 1
