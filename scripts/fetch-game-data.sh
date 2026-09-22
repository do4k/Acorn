#!/usr/bin/env bash
# Fetch the copyright-protected game data (dat*.edf, maps/*.emf, pub, ...) that
# is intentionally NOT tracked in git into src/Acorn/Data.
#
# The Docker image COPYies src/Acorn/Data at build time and docker-compose.yml
# bind-mounts the same directory at runtime. On a clean checkout only the 26
# tracked .cs files exist, so the server boots with 0 maps and disconnects
# every character (see issue #144). Run this before building/deploying.
#
# The files are served by Caddy from /srv/acorn/game-data - the same data the
# server hands to game clients. See deploy/caddy/Caddyfile (/gamedata path)
# and scripts/publish-game-data.sh for how the static copy is refreshed.
#
# Usage: scripts/fetch-game-data.sh [--force]
#   --force           re-download even if data is already present
# Env:
#   ACORN_GAME_DATA_URL   base URL (default https://acornhost.io/gamedata)
#   GAME_DATA_DEST        destination (default <repo>/src/Acorn/Data)
set -euo pipefail

BASE_URL="${ACORN_GAME_DATA_URL:-https://acornhost.io/gamedata}"
ROOT="$(git rev-parse --show-toplevel)"
DEST="${GAME_DATA_DEST:-$ROOT/src/Acorn/Data}"
ARCHIVE=acorn-game-data.tar.gz

FORCE=false
case "${1:-}" in
    --force) FORCE=true ;;
    "") ;;
    *) echo "usage: $0 [--force]" >&2; exit 2 ;;
esac

# Present = the ignored data files are there, not just the tracked .cs files.
data_present() {
    [ "$(find "$DEST" -name '*.emf' 2>/dev/null | wc -l)" -ge 100 ] &&
        [ "$(find "$DEST/Data" -name '*.edf' 2>/dev/null | wc -l)" -ge 5 ]
}

if [ "$FORCE" = false ] && data_present; then
    echo "Game data already present in $DEST ($(find "$DEST" -type f | wc -l) files); skipping (use --force to refetch)."
    exit 0
fi

tmp="$(mktemp -d)"
trap 'rm -rf "$tmp"' EXIT

echo "Downloading $BASE_URL/$ARCHIVE ..."
curl -fsSL --retry 3 -o "$tmp/$ARCHIVE" "$BASE_URL/$ARCHIVE"
curl -fsSL --retry 3 -o "$tmp/$ARCHIVE.sha256" "$BASE_URL/$ARCHIVE.sha256"

echo "Verifying checksum..."
(cd "$tmp" && sha256sum -c "$ARCHIVE.sha256")

mkdir -p "$DEST"
tar -xzf "$tmp/$ARCHIVE" -C "$DEST"

count="$(find "$DEST" -type f | wc -l | tr -d ' ')"
if [ "$count" -lt 400 ]; then
    echo "ERROR: expected at least 400 game-data files in $DEST, found $count." >&2
    exit 1
fi
if ! data_present; then
    echo "ERROR: $DEST still lacks maps (*.emf) or archives (Data/*.edf) after fetch." >&2
    exit 1
fi
echo "Game data ready: $DEST ($count files)."
