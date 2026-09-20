# Database Configuration

## Local Development (SQLite)

By default, when running locally with `dotnet run`, Acorn uses **SQLite** with the database file stored at `Acorn.db`.

Configuration in [appsettings.json](Acorn/appsettings.json):
```json
{
  "Database": {
    "Engine": "SQLite",
    "ConnectionString": "Data Source=Acorn.db;"
  }
}
```

## Docker Deployment (MySQL)

When running with `docker-compose up`, Acorn uses **MySQL** by default.

The MySQL database is automatically created and initialized with:
- Database: `acorn`
- User: `acorn`
- Password: `acornpassword`

### Running with Docker (MySQL - default)
```bash
docker compose --profile mysql up
```

### Running with Other Databases

Each database is a Compose profile that brings up a complete environment: the
database, the game server, the REST API and the Aspire dashboard. Set
`COMPOSE_PROFILES` in `.env` to choose the environment started by a bare
`docker compose up` (default: `mysql`).

#### SQLite
```bash
docker compose --profile sqlite up
```
(The server and API share the same `./data/sqlite` database file.)

#### PostgreSQL
```bash
docker compose --profile postgres up
```

#### SQL Server
```bash
docker compose --profile sqlserver up
```

## Account & Character Creation

Acorn does **not** seed a default account. Accounts are created by the client using the account-creation flow, and characters are created after logging in.

The first character is granted administrator rights when `Server:FirstCharacterAdmin` is enabled (default: `true`).

The server does not create or upgrade the schema at runtime — apply migrations before starting it (see [Migrations](#migrations)).

## Switching Database Engines

### For Local Development

Create environment-specific configuration files:

**appsettings.MySQL.json** (already exists):
```json
{
  "Database": {
    "Engine": "MySQL",
    "ConnectionString": "Server=localhost;Port=3306;Database=acorn;User=acorn;Password=acornpassword"
  }
}
```

Run with:
```bash
dotnet run --ASPNETCORE_ENVIRONMENT=MySQL
```

### Supported Database Engines

- **SQLite** - File-based, ideal for development and testing
- **MySQL** / **MariaDB** - Production-ready with excellent performance
- **PostgreSQL** - Advanced features and JSON support
- **SQL Server** - Enterprise-grade with Microsoft tooling

## Database Schema

The database uses **Entity Framework Core** with a relational structure:

### Migrations

Schema changes are managed with EF Core migrations in `src/Acorn.Database/Migrations`.
The server does not create or upgrade schemas at runtime, so apply migrations before starting it:

```bash
# dotnet-ef is a local tool; restore it once
dotnet tool restore

# Add a migration after changing the model
dotnet ef migrations add MigrationName --project src/Acorn.Database --startup-project src/Acorn

# Apply it to the development SQLite database
cd src/Acorn && dotnet ef database update --project ../Acorn.Database --startup-project .
```

`scripts/run-apphost.sh` applies migrations automatically. The provider and connection string can
be overridden with the `Database__Engine` / `Database__ConnectionString` environment variables.

> Migrations are generated against SQLite (the development default). Generate provider-specific
> migrations before deploying against MySQL/PostgreSQL/SQL Server.

#### PostgreSQL

PostgreSQL keeps its own migrations in `src/Acorn.Database.PostgreSql`. Selecting the PostgreSQL
engine sets `MigrationsAssembly` to that project, so the SQLite migrations are not used. Generate
and apply them with `ASPNETCORE_ENVIRONMENT=Production` (design-time scope validation otherwise
prevents the server's host from starting):

```bash
cd src/Acorn

# Add a migration
ASPNETCORE_ENVIRONMENT=Production Database__Engine=PostgreSQL \
  Database__ConnectionString="Host=localhost;Port=5432;Database=acorn;Username=acorn;Password=acornpassword" \
  dotnet ef migrations add MigrationName --project ../Acorn.Database.PostgreSql --startup-project .

# Apply it
ASPNETCORE_ENVIRONMENT=Production Database__Engine=PostgreSQL \
  Database__ConnectionString="Host=localhost;Port=5432;Database=acorn;Username=acorn;Password=acornpassword" \
  dotnet ef database update --project ../Acorn.Database.PostgreSql --startup-project .
```

MySQL and SQL Server still require their own migrations generated the same way.

### Tables
- **Accounts** - User accounts with authentication
- **Characters** - Character data (stats, position, etc.)
- **CharacterItems** - Inventory and bank items (relational)
- **CharacterPaperdolls** - Equipped items (15 slots)

### Relational Inventory

Items are stored in the `CharacterItems` table:
```sql
CREATE TABLE CharacterItems (
    Id INTEGER PRIMARY KEY,
    CharacterName TEXT NOT NULL,
    ItemId INTEGER NOT NULL,
    Amount INTEGER NOT NULL,
    Slot INTEGER NOT NULL  -- 0 = Inventory, 1 = Bank
);
```

This approach provides:
- ✅ No size limits on inventory
- ✅ Fast queries for specific items
- ✅ Data integrity with foreign keys
- ✅ Easy to add new item storage types

## Migration from Old Format

If you have existing data with serialized inventory strings, run the migration scripts in:
- [SQLite Migration](Acorn/Database/Scripts/SQLite/Migration_001_RelationalInventory.sql)
- [MSSQL Migration](Acorn/Database/Scripts/MSSQL/Migration_001_RelationalInventory.sql)

The old `Inventory`, `Bank`, and `Paperdoll` columns are no longer used.

## Troubleshooting

### Database connection errors
1. Check the connection string in your appsettings file
2. Ensure the database server is running (for MySQL/PostgreSQL/SQL Server)
3. Verify firewall rules allow connections
4. Check credentials are correct

### Database not initializing
The database is automatically created and migrated on startup. Check logs for:
```
Ensuring database is created and migrated...
Database initialized successfully
```

### Resetting the database
**SQLite:** Delete `Acorn.db` file and restart
**Docker:** `docker-compose down -v` to remove volumes, then `docker-compose up`
