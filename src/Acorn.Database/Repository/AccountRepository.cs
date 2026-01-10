using Acorn.Database.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Acorn.Database.Repository;

public class AccountRepository : IDbRepository<Account>
{
    private readonly AcornDbContext _context;
    private readonly ILogger<AccountRepository> _logger;

    public AccountRepository(
        AcornDbContext context,
        ILogger<AccountRepository> logger
    )
    {
        _context = context;
        _logger = logger;
    }

    public async Task CreateAsync(Account entity)
    {
        try
        {
            await _context.Accounts.AddAsync(entity);
            await _context.SaveChangesAsync();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error saving account information for {PlayerName}", entity.Username);
            throw;
        }
    }

    public async Task DeleteAsync(Account entity)
    {
        try
        {
            _context.Accounts.Remove(entity);
            await _context.SaveChangesAsync();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error deleting account {PlayerName}", entity.Username);
            throw;
        }
    }

    public async Task<Account?> GetByKeyAsync(string username)
    {
        try
        {
            var account = await _context.Accounts
                .Include(a => a.Characters)
                .ThenInclude(c => c.Paperdoll)
                .Include(a => a.Characters)
                .ThenInclude(c => c.Items)
                .FirstOrDefaultAsync(a => a.Username == username);

            if (account is null)
            {
                _logger.LogWarning("Account {Username} not found", username);
                return null;
            }

            return account;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error fetching account {Username}", username);
            return null;
        }
    }

    public async Task<IEnumerable<Account>> GetAllAsync()
    {
        try
        {
            return await _context.Accounts
                .Include(a => a.Characters)
                .ToListAsync();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error fetching all accounts");
            return [];
        }
    }

    public async Task UpdateAsync(Account entity)
    {
        try
        {
            _context.Accounts.Update(entity);
            await _context.SaveChangesAsync();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error updating account information for {PlayerName}", entity.Username);
            throw;
        }
    }
}
