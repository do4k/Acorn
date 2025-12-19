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
docker-compose up
```

### Running with Other Databases

#### SQLite
```bash
docker-compose --profile sqlite up acorn-sqlite
```

#### PostgreSQL
```bash
docker-compose --profile postgres up
```

#### SQL Server
```bash
docker-compose --profile sqlserver up
```

## Default Account & Character

On first startup, if no accounts or characters exist, Acorn will automatically create:

**Account:**
- Username: `acorn`
- Password: `acorn`

**Character:**
- Name: `acorn`
- Admin Level: High Game Master (5)
- Starting Location: Map 192 (6, 6)
- Level: 100
- All stats: 10

You can log in immediately with these credentials to test the server.

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
