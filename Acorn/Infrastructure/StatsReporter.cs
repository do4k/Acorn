using Acorn.Database.Models;
using Acorn.Database.Repository;
using Microsoft.Extensions.Logging;

namespace Acorn.Infrastructure;

public class StatsReporter : IStatsReporter
{
    private readonly IDbRepository<Account> _accountRepository;
    private readonly IDataFileRepository _dataRepository;
    private readonly ILogger<StatsReporter> _logger;

    public StatsReporter(
        ILogger<StatsReporter> logger,
        IDbRepository<Account> accountRepository,
        IDataFileRepository dataRepository
    )
    {
        _logger = logger;
        _accountRepository = accountRepository;
        _dataRepository = dataRepository;
    }

    public async Task Report()
    {
        var accounts = (await _accountRepository.GetAllAsync()).ToList();
        _logger.LogInformation("Loaded {Accounts} account(s)", accounts.Count);

        _logger.LogInformation("Loaded {Items} items", _dataRepository.Eif.Items.Count());
        _logger.LogInformation("Loaded {Npcs} npcs", _dataRepository.Enf.Npcs.Count());
        _logger.LogInformation("Loaded {Classes} classes", _dataRepository.Ecf.Classes.Count());
        _logger.LogInformation("Loaded {Skills} skills", _dataRepository.Esf.Skills.Count());
        _logger.LogInformation("Loaded {Maps} maps", _dataRepository.Maps.Count());
    }
}

public interface IStatsReporter
{
    Task Report();
}