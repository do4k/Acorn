using Acorn.Net.PacketHandlers.Player.Talk;
using FluentAssertions;

namespace Acorn.Tests.Integration;

/// <summary>
///     Guards the convention-based command registration: every handler must be
///     constructible from the real container, and no two handlers may claim the
///     same command name within a prefix.
/// </summary>
[ClassDataSource<TestServerFixture>(Shared = SharedType.PerClass)]
public class CommandRegistrationTests
{
    private readonly TestServerFixture _fixture;

    public CommandRegistrationTests(TestServerFixture fixture)
    {
        _fixture = fixture;
    }

    [Test]
    public void AdminCommandHandlers_ShouldAllResolve()
    {
        var handlers = _fixture.GetService<IEnumerable<ITalkHandler>>().ToList();

        handlers.Should().NotBeEmpty();
        handlers.Should().Contain(h => h.Commands.Count > 0);
    }

    [Test]
    public void PlayerCommandHandlers_ShouldAllResolve()
    {
        var handlers = _fixture.GetService<IEnumerable<IPlayerCommandHandler>>().ToList();

        handlers.Should().NotBeEmpty();
        handlers.Should().Contain(h => h.Commands.Count > 0);
    }

    [Test]
    public void AdminCommandAliases_ShouldBeUnique()
    {
        var duplicates = _fixture.GetService<IEnumerable<ITalkHandler>>()
            .SelectMany(h => h.Commands)
            .GroupBy(c => c, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        duplicates.Should().BeEmpty("each $ command name may be claimed by only one handler");
    }

    [Test]
    public void PlayerCommandAliases_ShouldBeUnique()
    {
        var duplicates = _fixture.GetService<IEnumerable<IPlayerCommandHandler>>()
            .SelectMany(h => h.Commands)
            .GroupBy(c => c, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        duplicates.Should().BeEmpty("each # command name may be claimed by only one handler");
    }
}
