# 🌰 Project Acorn

> A modern C# server emulator for Endless Online, built with .NET 10 and Entity Framework Core

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

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Docker](https://www.docker.com/) *(optional)*

### Run Locally

```bash
git clone https://github.com/do4k/acorn.git
cd acorn/src/Acorn
dotnet run
```

The server starts with **SQLite** by default—no database setup required.

> **No account is pre-created.** Register one from the Endless Online client's login screen on first run.

---

## 🐳 Docker

### Quick Start with Docker Compose

```bash
# MySQL (default)
docker-compose up

# SQLite
docker-compose --profile sqlite up acorn-sqlite

# PostgreSQL
docker-compose --profile postgres up

# SQL Server
docker-compose --profile sqlserver up
```

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

| Engine | Use Case | Configuration |
|--------|----------|---------------|
| **SQLite** | Development, testing | `dotnet run` (default) |
| **MySQL** | Docker default, production | `docker-compose up` |
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

```bash
# Create a migration
dotnet ef migrations add MigrationName

# Apply migrations (automatic on startup)
dotnet ef database update

# Rollback
dotnet ef database update PreviousMigrationName
```

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
