#!/usr/bin/env bash
# Publish the local game data to the directory Caddy serves statically
# (mounted into the caddy container as /srv/gamedata, exposed under
# https://<BASE_DOMAIN>/gamedata/). Refresh after the data files change.
#
# Usage: scripts/publish-game-data.sh
# Env:
#   GAME_DATA_SOURCE       source dir (default <repo>/src/Acorn/Data)
#   GAME_DATA_PUBLISH_DIR  destination (default /srv/acorn/game-data)
set -euo pipefail

ROOT="$(git rev-parse --show-toplevel)"
SRC="${GAME_DATA_SOURCE:-$ROOT/src/Acorn/Data}"
DEST="${GAME_DATA_PUBLISH_DIR:-/srv/acorn/game-data}"
ARCHIVE=acorn-game-data.tar.gz

count="$(find "$SRC" -type f 2>/dev/null | wc -l | tr -d ' ')"
if [ "$count" -lt 400 ]; then
    echo "ERROR: $SRC has only $count files (expected >= 400); refusing to publish an empty data set." >&2
    exit 1
fi

mkdir -p "$DEST"

echo "Packaging $SRC ($count files)..."
tar -czf "$DEST/$ARCHIVE" -C "$SRC" .
(cd "$DEST" && sha256sum "$ARCHIVE" > "$ARCHIVE.sha256")

echo "Mirroring raw files to $DEST/files ..."
if command -v rsync >/dev/null 2>&1; then
    rsync -a --delete "$SRC/" "$DEST/files/"
else
    rm -rf "$DEST/files"
    mkdir -p "$DEST/files"
    cp -a "$SRC/." "$DEST/files/"
fi

echo "Published:"
ls -la "$DEST/$ARCHIVE" "$DEST/$ARCHIVE.sha256"
echo "  files/: $(find "$DEST/files" -type f | wc -l | tr -d ' ') files"
echo "Fetch URL: https://<BASE_DOMAIN>/gamedata/$ARCHIVE"
