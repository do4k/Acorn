# Project Acorn
## Overview

**Project Acorn** - The C# Endless Online Server Emulator

![ai logo](docs/ai-logo.jpg)

![acorn screenshot](docs/screenshot.png)

## Features

- ✅ **Entity Framework Core** with support for multiple databases
- ✅ **SQLite** - Lightweight, file-based database (default)
- ✅ **PostgreSQL** - Production-ready RDBMS
- ✅ **MySQL/MariaDB** - Popular open-source database
- ✅ **SQL Server** - Microsoft's enterprise database
- ✅ **Docker Support** - Containerized deployment with docker-compose
- ✅ **Improved NPC System** - Randomized movement based on eoserv spawn types
- ✅ **WebSocket & TCP** - Dual protocol support
- ✅ **Entity Framework Migrations** - Easy database schema management

## Prerequisites

- [.NET 9.0 SDK](https://dotnet.microsoft.com/download) or higher
- [Docker](https://www.docker.com/) (optional, for containerized deployment)
- Database server (optional, SQLite is embedded)

## Quick Start

### Running Locally with SQLite (Default)

1. Clone the repository:
    ```sh
    git clone https://github.com/yourusername/project-acorn.git
    cd project-acorn
    ```

2. Restore dependencies:
    ```sh
    dotnet restore
    ```

3. Run the application:
    ```sh
    cd Acorn
    dotnet run
    ```

The server will start with SQLite by default. Database will be created automatically.

**Default Login Credentials:**
- Username: `acorn`
- Password: `acorn`

### Running with Docker

#### MySQL (Default)
```sh
docker-compose up
```

#### SQLite
```sh
docker-compose --profile sqlite up acorn-sqlite
```

#### PostgreSQL
```sh
docker-compose --profile postgres up
```

#### SQL Server
```sh
docker-compose --profile sqlserver up
```

## Database Configuration

**Local Development:** Uses **SQLite** (file-based, no setup required)  
**Docker Deployment:** Uses **MySQL** (production-ready)

On first startup, the server automatically creates:
- Default account: `acorn` / `acorn`
- Default character: `acorn` (Admin level, Map 192)

For detailed database configuration, see [docs/DATABASE.md](docs/DATABASE.md)

### Switching Databases Locally

Use different appsettings files:

```sh
# PostgreSQL
dotnet run --environment=PostgreSQL

# MySQL
dotnet run --environment=MySQL

# SQL Server
dotnet run --environment=SqlServer
```

Or modify `appsettings.json`:

```json
{
  "Database": {
    "Engine": "PostgreSQL",
    "ConnectionString": "Host=localhost;Database=acorn;Username=acorn;Password=password;"
  }
}
```

Supported engines: `SQLite`, `PostgreSQL`, `MySQL`, `SqlServer`

## Entity Framework Core Migrations

### Creating a New Migration

```sh
cd Acorn
dotnet ef migrations add YourMigrationName
```

### Applying Migrations

Migrations are automatically applied on startup. To apply manually:

```sh
dotnet ef database update
```

### Rolling Back

```sh
dotnet ef database update PreviousMigrationName
```

## NPC Behavior System

NPCs now use spawn types based on eoserv's behavior system:

- **Type 0-6**: Different movement speeds (0.6s to 15s between actions)
- **Type 7**: Stationary NPCs that never move
- **Random Walking**: 60% walk forward, 30% change direction, 10% idle
- **Individual Behaviors**: Each NPC has its own movement pattern
- **Spawn Randomization**: NPCs spawn within 2 tiles of their spawn point

## Building the Project

```sh
dotnet build
```

For release builds:

```sh
dotnet build -c Release
```

## Docker Build

### Build Multi-Platform Images

```sh
docker buildx build --platform linux/amd64,linux/arm64 -t acorn:latest ./Acorn
```

### Development Build

```sh
docker build -t acorn:dev ./Acorn
```

## Contributing

We welcome contributions! Please see our [contributing guidelines](CONTRIBUTING.md) for more details.

## License

This project is licensed under the MIT License. See the LICENSE file for details.

## Acknowledgements

- **EthanMoffat:** `eolib-dotnet` and `EndlessClient`
- **Cirras:** `eo-protocol`
- **Sorokya:** `reoserv`
- **Sausage:** `EOSERV`
- **Vult-r:** Original software
