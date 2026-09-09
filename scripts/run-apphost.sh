#!/usr/bin/env bash
#
# Starts the full Acorn stack via the .NET Aspire AppHost:
#   - acorn-server (game server: TCP 8078 / WebSocket 8079)
#   - acorn-api    (REST API)
#   - Aspire dashboard (prints its URL + login token)
#
# The stack initialises the database and comes up in one command.
# Run in the foreground; press Ctrl+C to stop everything.
#
# Usage:
#   scripts/run-apphost.sh                # build then run
#   scripts/run-apphost.sh --no-build     # run without rebuilding
#   DOTNET_DIR=/custom/path scripts/run-apphost.sh
#
set -euo pipefail

# Resolve the repo root regardless of where the script is invoked from.
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

# Locate the .NET 11 SDK. This project installs its own SDK to ~/.dotnet.
DOTNET_DIR="${DOTNET_DIR:-$HOME/.dotnet}"
if [[ -x "$DOTNET_DIR/dotnet" ]]; then
    export PATH="$DOTNET_DIR:$PATH"
    export DOTNET_ROOT="$DOTNET_DIR"
    echo "Using .NET SDK from: $DOTNET_DIR"
fi

command -v dotnet >/dev/null 2>&1 || {
    echo "error: 'dotnet' not found on PATH ($DOTNET_DIR)." >&2
    echo "       Set DOTNET_DIR to your .NET 11 SDK directory and retry." >&2
    exit 1
}

# Warn (but don't block) if the resolved SDK isn't an 11.x preview/RC.
SDK_VERSION="$(dotnet --version)"
case "$SDK_VERSION" in
    11.*)  echo "Detected .NET SDK ${SDK_VERSION}" ;;
    *)     echo "warning: expected a .NET 11 SDK but found ${SDK_VERSION}." >&2 ;;
esac

# ---------------------------------------------------------------------------
# Trust the ASP.NET Core HTTPS dev certificate for OpenSSL.
#
# `dotnet dev-certs https --trust` only works on Windows/macOS; on Linux the
# cert is generated but OpenSSL is never told to trust it. This breaks the
# Aspire dashboard's internal gRPC resource-service connection (and OTLP ingest)
# with 'UntrustedRoot'. `aspire run` wires this up automatically, but plain
# `dotnet run` does not - so we do it here.
# ---------------------------------------------------------------------------
setup_dev_cert_trust() {
    local trust_dir="${HOME}/.aspnet/dev-certs/trust"
    mkdir -p "$trust_dir"

    if [[ ! -f "$trust_dir/localhost.crt" ]]; then
        echo "Exporting the ASP.NET Core dev certificate for OpenSSL trust..."
        dotnet dev-certs https -ep "$trust_dir/localhost.pfx" -p acorn-dev-cert >/dev/null 2>&1 || true
        openssl pkcs12 -in "$trust_dir/localhost.pfx" -clcerts -nokeys \
            -out "$trust_dir/localhost.crt" -passin pass:acorn-dev-cert >/dev/null 2>&1 || true
    fi

    # Make OpenSSL (and .NET's TLS) able to look the cert up by its hash.
    openssl rehash "$trust_dir" >/dev/null 2>&1 || true

    # Point OpenSSL at both the Aspire dev-cert trust dir and the system CA store.
    local system_certs=""
    for d in /etc/ssl/certs /etc/pki/tls/certs /usr/lib/ssl/certs; do
        if [[ -d "$d" ]]; then
            system_certs="$d"
            break
        fi
    done

    export SSL_CERT_DIR="$trust_dir${system_certs:+:$system_certs}"
    echo "SSL_CERT_DIR=$SSL_CERT_DIR"
}

if command -v openssl >/dev/null 2>&1; then
    setup_dev_cert_trust
else
    echo "warning: openssl not found - the Aspire dashboard's resource view may not connect (UntrustedRoot)." >&2
fi

cd "$ROOT"
echo "Starting Acorn stack from: $ROOT"

# Note: the AppHost rebuilds referenced projects on demand, so this works from a clean clone too.
exec dotnet run --project src/Acorn.AppHost "$@"
