using System.Data;
using Acorn.Database.Models;
using Dapper;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Acorn.Database.Repository;

public class CharacterRepository : BaseDbRepository, IDbRepository<Character>, IDisposable
{
    private readonly IDbConnection _conn;
    private readonly ILogger<AccountRepository> _logger;

    public CharacterRepository(
        IDbConnection conn,
        ILogger<AccountRepository> logger,
        IOptions<DatabaseOptions> options,
        IDbInitialiser initialiser
    ) : base(initialiser)
    {
        _conn = conn;
        _logger = logger;

        SQLStatements.Create = File.ReadAllText($"Database/Scripts/{options.Value.Engine}/Character/Create.sql");
        SQLStatements.Update = File.ReadAllText($"Database/Scripts/{options.Value.Engine}/Character/Update.sql");
        SQLStatements.GetByKey = File.ReadAllText($"Database/Scripts/{options.Value.Engine}/Character/GetByKey.sql");

        if (_conn.State != ConnectionState.Open)
        {
            _conn.Open();
        }
    }

    public async Task CreateAsync(Character entity)
    {
        using var t = _conn.BeginTransaction(IsolationLevel.ReadCommitted);
        try
        {
            await _conn.ExecuteAsync(SQLStatements.Create, entity);
            t.Commit();
        }
        catch (Exception e)
        {
            _logger.LogError("Error saving character information for {CharacterName}. Exception {Exception}",
                entity.Name, e.Message);
            t.Rollback();
        }
    }

    public Task DeleteAsync(Character entity)
    {
        throw new NotImplementedException();
    }

    public async Task<Character?> GetByKeyAsync(string name)
    {
        try
        {
            var characters = (await _conn.QueryAsync<Character>(
                SQLStatements.GetByKey, new { name })).ToList();

            if (characters.Count > 0)
            {
                return characters.First();
            }
            _logger.LogInformation("Character {Character} not found", name);
            return null;
        }
        catch (Exception e)
        {
            _logger.LogError("Error fetching character {Character}. Exception {Exception}", name, e.Message);
        }

        return null;
    }

    public async Task UpdateAsync(Character entity)
    {
        using var t = _conn.BeginTransaction(IsolationLevel.ReadCommitted);
        try
        {
            await _conn.ExecuteAsync(SQLStatements.Update, entity);

            _logger.LogInformation("Saved character '{Name}'", entity.Name);

            t.Commit();
        }
        catch (Exception e)
        {
            _logger.LogError("Error saving character information for {CharacterName}. Exception {Exception}",
                entity.Name, e.Message);
            t.Rollback();
        }
    }

    public Task<IEnumerable<Character>> GetAllAsync()
    {
        throw new NotImplementedException();
    }

    public void Dispose()
    {
        if (_conn.State == ConnectionState.Open)
        {
            _conn.Close();
        }

        _conn.Dispose();
    }

    public static class SQLStatements
    {
        public static string Create = "";
        public static string Update = "";
        public static string GetByKey = "";
    }
}