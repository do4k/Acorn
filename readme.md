# 🌰 Project Acorn

> A modern C# server emulator for Endless Online, built with .NET 11 and Entity Framework Core

```
          _          Acorn Endless-Online Server Software   
        _/-\_ 
    .-`-:-:-`-.
    /-:-:-:-:-:-\
    \:-:-:-:-:-:/ 
     |`   ,   `|  
     |   (     |
     `\   `   /'
       `-._.-'
```

![acorn screenshot](docs/screenshot.png)

---

## ✨ Features

| Category | Features |
|----------|----------|
| **Database** | Entity Framework Core 10, SQLite, PostgreSQL, MySQL/MariaDB, SQL Server |
| **Networking** | WebSocket & TCP dual protocol support |
| **Deployment** | Docker multi-arch images (amd64/arm64), docker-compose profiles |
| **NPCs** | Randomized movement system based on eoserv spawn types |
| **CI/CD** | GitHub Actions for builds, tests, and container publishing |

---

## 🚀 Quick Start

### Prerequisites

- [.NET 11 SDK](https://dotnet.microsoft.com/download)
- [Docker](https://www.docker.com/) *(optional)*

### Run Locally

The server does not create or upgrade the schema at runtime, so apply the SQLite
migrations once before the first run:

```bash
git clone https://github.com/do4k/acorn.git
cd acorn
dotnet tool restore

cd src/Acorn
dotnet ef database update --project ../Acorn.Database --startup-project .
dotnet run
```

The server then starts with **SQLite** by default.

> **No account is pre-created.** Register one from the Endless Online client's login screen on first run.

---

## 🐳 Docker

### Quick Start with Docker Compose

Each database is a Compose profile that brings up a complete environment: the
database, the game server (TCP `:8078` / WebSocket `:8079`), the REST API
(`:5000`) and the Aspire dashboard (`:18888`). The dashboard runs for every
profile.

`COMPOSE_PROFILES` in `.env` selects the environment started by a bare
`docker compose up` (default: `mysql`).

```bash
# Default environment from COMPOSE_PROFILES
docker compose up

# Or choose an environment explicitly
docker compose --profile mysql up
docker compose --profile sqlite up
docker compose --profile postgres up
docker compose --profile sqlserver up
```

### Apply Database Migrations

`docker compose up` does **not** apply EF Core migrations. The server and API
never create or upgrade the schema at runtime, so apply migrations once before
starting the app containers. Each environment publishes its database port on
the host, so run them from the repository root with the local .NET SDK.

The example below uses the PostgreSQL environment. Point `COMPOSE_PROFILES` in
`.env` at the environment you want (`COMPOSE_PROFILES=postgres`); for a
different engine, use its profile in step 1/3 and its provider in step 2.

```bash
# 1. Start just the database container
docker compose up -d postgres

# 2. Apply that engine's migrations
dotnet tool restore
cd src/Acorn
ASPNETCORE_ENVIRONMENT=Production \
Database__Engine=PostgreSQL \
Database__ConnectionString="Host=localhost;Port=5432;Database=acorn;Username=acorn;Password=acornpassword" \
  dotnet ef database update --project ../Acorn.Database.PostgreSql --startup-project .

# 3. Start the rest of the environment
cd ../..
docker compose up -d
```

`ASPNETCORE_ENVIRONMENT=Production` matters here: in Development the server
enables scope validation, which the design-time tooling cannot build.

> Use the provider that matches the profile. SQLite and PostgreSQL ship
> migrations in this repository; MySQL and SQL Server need their own generated
> first — see [docs/DATABASE.md](docs/DATABASE.md#migrations).

### Web Client, TLS & Domains

A `caddy` service terminates TLS and reverse-proxies, so it runs for every
profile. Site hostnames are derived from `BASE_DOMAIN` in `.env`:

- `<BASE_DOMAIN>` — static landing page (`deploy/landing`)
- `game.<BASE_DOMAIN>` — the eoweb browser client (`deploy/eoweb`) and the
  WebSocket endpoint. `wss://game.<BASE_DOMAIN>` is proxied to the game server's
  WebSocket port (`8079`), which is bound to loopback and not exposed directly.
- `aspire.<BASE_DOMAIN>` — the Aspire dashboard, behind basic auth, served only
  when `ASPIRE_ENABLED=true`. Credentials come from `ASPIRE_USER` /
  `ASPIRE_PASSWORD_HASH` (bcrypt; escape each `$` as `$$`).

All hostnames must resolve to the host and ports `80`/`443` must be reachable
so Caddy can obtain and renew Let's Encrypt certificates. Native clients still
connect directly to TCP `8078`.

`deploy/eoweb` is a placeholder. To deploy a real client, point `EOWEB_DIST` in
`.env` at a built [sorokya/eoweb](https://github.com/sorokya/eoweb) `dist/`
(including its `data`, `gfx`, `sfx`, `mfx`, `jbox` and `maps` folders). That
keeps the large asset set out of the repository.

### Pull from GitHub Container Registry

```bash
docker pull ghcr.io/do4k/acorn:latest

docker run -p 8078:8078 -p 8079:8079 ghcr.io/do4k/acorn:latest
```

### Build Locally

```bash
# Development
docker build -t acorn:dev ./src/Acorn

# Multi-platform
docker buildx build --platform linux/amd64,linux/arm64 -t acorn:latest ./src/Acorn
```

---

## 🗄️ Database Configuration

### Supported Databases

| Engine | Use Case | Docker profile |
|--------|----------|----------------|
| **SQLite** | Development, testing | `--profile sqlite` |
| **MySQL** | Default Docker environment | `--profile mysql` |
| **PostgreSQL** | Production with JSON support | `--profile postgres` |
| **SQL Server** | Enterprise environments | `--profile sqlserver` |

### Switching Databases Locally

```bash
dotnet run --environment=PostgreSQL
dotnet run --environment=MySQL
dotnet run --environment=SqlServer
```

Or edit `appsettings.json`:

```json
{
  "Database": {
    "Engine": "PostgreSQL",
    "ConnectionString": "Host=localhost;Database=acorn;Username=acorn;Password=password;"
  }
}
```

### Entity Framework Migrations

The server does not create or upgrade the schema at runtime; migrations are
always applied explicitly.

```bash
dotnet tool restore

# Add a migration after changing the model
dotnet ef migrations add MigrationName --project src/Acorn.Database --startup-project src/Acorn

# Apply migrations to the development SQLite database
cd src/Acorn
dotnet ef database update --project ../Acorn.Database --startup-project .

# Rollback
dotnet ef database update PreviousMigrationName --project ../Acorn.Database --startup-project .
```

PostgreSQL uses its own migrations assembly (`Acorn.Database.PostgreSql`) and
the commands differ; see
[docs/DATABASE.md](docs/DATABASE.md#migrations) for provider-specific details.

> 📖 See [docs/DATABASE.md](docs/DATABASE.md) for detailed configuration options.

---

## 🎮 First Login

Acorn does **not** seed a default account. The first account and character are created from the Endless Online client: use the login screen's "Create account" flow, then create a character.

The first character is granted administrator rights when `Server:FirstCharacterAdmin` is enabled (default: `true`).

---

## 🤖 NPC Behavior System

NPCs use spawn types based on eoserv's behavior system:

- **Types 0-6** — Movement speeds from 0.6s to 15s
- **Type 7** — Stationary (never moves)
- **Movement** — 60% walk, 30% turn, 10% idle
- **Spawn** — Randomized within 2 tiles of spawn point

---

## 🛠️ Development

### Build

```bash
dotnet build              # Debug
dotnet build -c Release   # Release
```

### Test

```bash
dotnet test
```

---

## 🙏 Acknowledgements

- **[EthanMoffat](https://github.com/ethanmoffat)** — eolib-dotnet, EndlessClient
- **[Cirras](https://github.com/cirras)** — eo-protocol
- **[Sorokya](https://github.com/sorokya)** — reoserv
- **Sausage** — EOSERV
- **Vult-r** — Original software

---

## 📄 License

MIT License — see [LICENSE](LICENSE) for details.
