#!/bin/sh
# Assembles the Caddy config from the base Caddyfile and, when enabled, the
# Aspire dashboard fragment, then runs Caddy in the foreground.
#
# Configures via environment:
#   BASE_DOMAIN            apex domain; sites are <base>, game.<base>, aspire.<base>
#   ASPIRE_ENABLED         true|false (default true) - expose the Aspire dashboard
#   ASPIRE_USER            basic-auth username (default acorn)
#   ASPIRE_PASSWORD_HASH   bcrypt hash for the basic-auth password

set -eu

generated=/etc/caddy/Caddyfile.generated

cat /etc/caddy/Caddyfile > "$generated"

if [ "${ASPIRE_ENABLED:-true}" = "true" ]; then
	printf '\n' >> "$generated"
	cat /etc/caddy/aspire.caddy >> "$generated"
fi

exec caddy run --config "$generated" --adapter caddyfile
