using Acorn.Database.Repository;
using Acorn.World.Services.Admin;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moffat.EndlessOnline.SDK.Protocol;
using NSubstitute;
using Xunit;
using DbCharacter = Acorn.Database.Models.Character;

namespace Acorn.Tests.World.Services.Admin;

public class AdminCountServiceTests
{
    private static AdminCountService CreateSut(params DbCharacter[] characters)
    {
        var repository = Substitute.For<IDbRepository<DbCharacter>>();
        repository.GetAllAsync().Returns(characters);

        var provider = Substitute.For<IServiceProvider>();
        provider.GetService(typeof(IDbRepository<DbCharacter>)).Returns(repository);

        var scope = Substitute.For<IServiceScope>();
        scope.ServiceProvider.Returns(provider);

        var scopeFactory = Substitute.For<IServiceScopeFactory>();
        scopeFactory.CreateScope().Returns(scope);

        return new AdminCountService(scopeFactory, NullLogger<AdminCountService>.Instance);
    }

    private static DbCharacter Character(AdminLevel admin) => new()
    {
        Accounts_Username = "tester",
        Name = $"char{(int)admin}",
        Admin = admin
    };

    [Fact]
    public async Task GetAdminCountAsync_ShouldCountOnlyAdmins()
    {
        var sut = CreateSut(
            Character(AdminLevel.HighGameMaster),
            Character(AdminLevel.Player),
            Character(AdminLevel.GameMaster));

        var count = await sut.GetAdminCountAsync();

        count.Should().Be(2);
    }

    [Fact]
    public async Task IncrementAndDecrement_ShouldAdjustCachedCount()
    {
        var sut = CreateSut(Character(AdminLevel.Player));

        (await sut.GetAdminCountAsync()).Should().Be(0);

        sut.Increment();
        (await sut.GetAdminCountAsync()).Should().Be(1);

        sut.Decrement();
        (await sut.GetAdminCountAsync()).Should().Be(0);

        sut.Decrement();
        (await sut.GetAdminCountAsync()).Should().Be(0, "the count should never go negative");
    }
}
