using System.Data;
using Acorn.Database.Models;
using Dapper;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Acorn.Database.Repository;

public class AccountRepository : BaseDbRepository, IDbRepository<Account>, IDisposable
{
    private readonly IDbConnection _conn;
    private readonly ILogger<AccountRepository> _logger;

    public AccountRepository(
        IDbConnection conn,
        ILogger<AccountRepository> logger,
        IOptions<DatabaseOptions> options,
        IDbInitialiser initialiser
    ) : base(initialiser)
    {
        _conn = conn;
        _logger = logger;

        SQLStatements.Create = File.ReadAllText($"Database/Scripts/{options.Value.Engine}/Account/Create.sql");
        SQLStatements.Update = File.ReadAllText($"Database/Scripts/{options.Value.Engine}/Account/Update.sql");
        SQLStatements.GetByKey = File.ReadAllText($"Database/Scripts/{options.Value.Engine}/Account/GetByKey.sql");
        SQLStatements.Delete = File.ReadAllText($"Database/Scripts/{options.Value.Engine}/Account/Delete.sql");
        SQLStatements.GetCharacters =
            File.ReadAllText($"Database/Scripts/{options.Value.Engine}/Account/GetCharacters.sql");
        SQLStatements.GetAll = File.ReadAllText($"Database/Scripts/{options.Value.Engine}/Account/GetAll.sql");

        if (_conn.State != ConnectionState.Open)
        {
            _conn.Open();
        }
    }

    public async Task CreateAsync(Account entity)
    {
        using var t = _conn.BeginTransaction(IsolationLevel.ReadCommitted);
        try
        {
            await _conn.ExecuteAsync(SQLStatements.Create, entity);

            t.Commit();
        }
        catch (Exception e)
        {
            _logger.LogError("Error saving account information for {PlayerName}. Exception {Exception}",
                entity.Username, e.Message);
            t.Rollback();
        }
    }

    public async Task DeleteAsync(Account entity)
    {
        using var t = _conn.BeginTransaction();
        try
        {
            await _conn.ExecuteAsync(SQLStatements.Delete, new { entity.Username });
        }
        catch (Exception e)
        {
            _logger.LogError("Error deleting account {PlayerName}. Exception {Exception}", entity.Username, e.Message);
            t.Rollback();
        }
    }

    public async Task<Account?> GetByKeyAsync(string username)
    {
        try
        {
            var acc = await _conn.QuerySingleOrDefaultAsync<Account>(SQLStatements.GetByKey, new { username });
            if (acc is null)
            {
                _logger.LogWarning("Account {Username} not found", username);
                return null;
            }

            acc.Characters = (await _conn.QueryAsync<Character>(SQLStatements.GetCharacters, new { username }))
                .ToList();
            return acc;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error fetching account {Username}", username);
        }

        return null;
    }

    public async Task<IEnumerable<Account>> GetAllAsync()
    {
        try
        {
            var accounts = (await _conn.QueryAsync<Account>(SQLStatements.GetAll)).ToList();
            var withCharacters = accounts.Select(async a =>
            {
                a.Characters =
                    (await _conn.QueryAsync<Character>(SQLStatements.GetCharacters, new { username = a.Username }))
                    .ToList();
                return a;
            });

            return await Task.WhenAll(withCharacters);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error fetching all accounts");
        }

        return [];
    }

    public async Task UpdateAsync(Account entity)
    {
        using var t = _conn.BeginTransaction(IsolationLevel.ReadCommitted);
        try
        {
            await _conn.ExecuteAsync(SQLStatements.Update, entity);
            t.Commit();
        }
        catch (Exception e)
        {
            _logger.LogError("Error saving account information for {PlayerName}. Exception {Exception}",
                entity.Username, e.Message);
            t.Rollback();
        }
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
        public static string Delete = "";
        public static string GetCharacters = "";
        public static string GetAll = "";
    }
}