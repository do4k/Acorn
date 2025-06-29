using System.Net;
using System.Net.Sockets;
using Acorn.Database.Models;
using Acorn.Database.Repository;
using Acorn.Infrastructure;
using Acorn.Options;
using Acorn.World;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Acorn.Net;

public class NewConnectionHostedService(
    IServiceProvider services,
    ILogger<NewConnectionHostedService> logger,
    ILogger<ConnectionHandler> playerConnectionLogger,
    IStatsReporter statsReporter,
    WorldState worldState,
    IDbRepository<Character> characterRepository,
    ISessionGenerator sessionGenerator,
    IOptions<ServerOptions> serverOptions
) : BackgroundService, IDisposable
{
    private readonly IDbRepository<Character> _characterRepository = characterRepository;
    private readonly TcpListener _listener = new(IPAddress.Any, serverOptions.Value.Port);
    private readonly ILogger<NewConnectionHostedService> _logger = logger;
    private readonly ILogger<ConnectionHandler> _playerConnectionLogger = playerConnectionLogger;
    private readonly IServiceProvider _services = services;
    private readonly IStatsReporter _statsReporter = statsReporter;
    private readonly WorldState _world = worldState;

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        await _statsReporter.Report();
        _listener.Start();
        _logger.LogInformation("Waiting for connections on {Endpoint}...", _listener.LocalEndpoint);
        while (cancellationToken.IsCancellationRequested is false)
        {
            var client = await _listener.AcceptTcpClientAsync(cancellationToken);
            var sessionId = sessionGenerator.Generate();
            var added = _world.Players.TryAdd(sessionId, new ConnectionHandler(_services, client, _playerConnectionLogger, sessionId,
                async (player) =>
                {
                    if (player.CharacterController is not null)
                    {
                        if (player.CurrentMap is not null)
                        {
                            await player.CurrentMap.NotifyLeave(player);
                            await player.CharacterController.Save();
                        }
                    }

                    var removed = _world.Players.TryRemove(sessionId, out var removedConnection);
                    if (removed is false)
                    {
                        _logger.LogWarning("Failed to remove player connection with session ID {SessionId}", player.SessionId);
                        return;
                    }

                    _logger.LogInformation("ConnectionHandler disconnected");
                    UpdateConnectedCount();
                }));

            _logger.LogInformation("Connection accepted. {PlayersConnected} players connected", _world.Players.Count);
            UpdateConnectedCount();
        }
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        Dispose();
        return base.StopAsync(cancellationToken);
    }

    private void UpdateConnectedCount()
    {
        Console.Title = $"Acorn Server ({_world.Players.Count} Connected)";
    }

    public override void Dispose()
    {
        _listener.Stop();
        _listener.Dispose();
        base.Dispose();
    }
}