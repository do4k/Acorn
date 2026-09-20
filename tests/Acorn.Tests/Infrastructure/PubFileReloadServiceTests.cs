using Acorn.Database.Repository;
using Acorn.Infrastructure;
using Acorn.Shared.Caching;
using Acorn.Shared.Models.Pub;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moffat.EndlessOnline.SDK.Protocol.Pub;
using NSubstitute;
using System.Threading.Tasks;

namespace Acorn.Tests.Infrastructure;

public class PubFileReloadServiceTests
{
    private static (PubFileReloadService Sut, IDataFileRepository DataFiles, IPubCacheService PubCache) CreateSut()
    {
        var dataFiles = Substitute.For<IDataFileRepository>();
        dataFiles.Eif.Returns(new Eif { Items = new List<EifRecord>() });
        dataFiles.Enf.Returns(new Enf { Npcs = new List<EnfRecord>() });
        dataFiles.Esf.Returns(new Esf { Skills = new List<EsfRecord>() });
        dataFiles.Ecf.Returns(new Ecf { Classes = new List<EcfRecord>() });

        var pubCache = Substitute.For<IPubCacheService>();
        var sut = new PubFileReloadService(dataFiles, pubCache, NullLogger<PubFileReloadService>.Instance);

        return (sut, dataFiles, pubCache);
    }

    [Test]
    public async Task RefreshCache_ShouldCacheAllPubTypes()
    {
        var (sut, _, pubCache) = CreateSut();

        await sut.RefreshCacheAsync();

        await pubCache.Received(1).CacheItemsAsync(Arg.Any<IEnumerable<ItemRecord>>());
        await pubCache.Received(1).CacheNpcsAsync(Arg.Any<IEnumerable<NpcRecord>>());
        await pubCache.Received(1).CacheSpellsAsync(Arg.Any<IEnumerable<SpellRecord>>());
        await pubCache.Received(1).CacheClassesAsync(Arg.Any<IEnumerable<ClassRecord>>());
    }

    [Test]
    public async Task Reload_ShouldReloadDataFilesThenRefreshCache()
    {
        var (sut, dataFiles, pubCache) = CreateSut();

        var result = await sut.ReloadAsync();

        result.Should().BeTrue();
        dataFiles.Received(1).Reload();
        await pubCache.Received(1).CacheItemsAsync(Arg.Any<IEnumerable<ItemRecord>>());
    }

    [Test]
    public async Task Reload_WhenDataFilesThrow_ShouldReturnFalseAndSkipCaching()
    {
        var (sut, dataFiles, pubCache) = CreateSut();
        dataFiles.When(x => x.Reload()).Do(_ => throw new InvalidOperationException("boom"));

        var result = await sut.ReloadAsync();

        result.Should().BeFalse();
        await pubCache.DidNotReceiveWithAnyArgs().CacheItemsAsync(default!);
    }
}
