#!/usr/bin/env bash
#
# Tail the game server's log file (the file-logging fallback).
# The Aspire dashboard resource-log view can be empty/flaky on Linux, so the
# server also writes logs to a file you can watch with this helper.
#
# Usage:
#   scripts/tail-logs.sh                    # tail src/Acorn/acorn.log
#   ACORN_LOG_FILE=/path/to/acorn.log scripts/tail-logs.sh
#
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
LOG_FILE="${ACORN_LOG_FILE:-$ROOT/src/Acorn/acorn.log}"

if [[ ! -f "$LOG_FILE" ]]; then
    echo "Log file not found yet: $LOG_FILE" >&2
    echo "Start the stack first (scripts/run-apphost.sh), then re-run this." >&2
    exit 1
fi

echo "Tailing $LOG_FILE (Ctrl+C to stop)"
exec tail -f "$LOG_FILE"
