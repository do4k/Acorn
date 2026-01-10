using Microsoft.Extensions.Logging;

namespace Acorn.Database.Repository;

public class DbInitialiser : IDbInitialiser
{
    private readonly AcornDbContext _context;
    private readonly ILogger<DbInitialiser> _logger;

    public DbInitialiser(AcornDbContext context, ILogger<DbInitialiser> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task InitialiseAsync()
    {
        try
        {
            _logger.LogInformation("Ensuring database is created...");

            // EnsureCreatedAsync creates the database schema if it doesn't exist
            // Note: This won't apply migrations, use MigrateAsync() if you add EF migrations
            var created = await _context.Database.EnsureCreatedAsync();
            if (created)
            {
                _logger.LogInformation("Database schema created successfully");
            }
            else
            {
                _logger.LogInformation("Database schema already exists");
            }

            _logger.LogInformation("Database initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing database");
            throw;
        }
    }
}

public interface IDbInitialiser
{
    Task InitialiseAsync();
}
