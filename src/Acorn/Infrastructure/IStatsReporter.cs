using Acorn.Database.Models;
using Acorn.Database.Repository;
using Microsoft.Extensions.Logging;

namespace Acorn.Infrastructure;

public interface IStatsReporter
{
    Task Report();
}