using System.Reflection;
using Acorn.Data;
using Acorn.Database;
using Acorn.Database.Models;
using Acorn.Database.Repository;
using Acorn.Game.Services;
using Acorn.Net.PacketHandlers;
using Acorn.Shared.Caching;
using Acorn.World.Services.Admin;
using Acorn.World.Services.Map;
using Acorn.World.Services.Arena;
using Acorn.World.Services.Guild;
using Acorn.World.Services.Marriage;
using Acorn.World.Services.Quest;
using Acorn.World.Services.Npc;
using Acorn.World.Services.Party;
using Acorn.World.Services.Player;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Acorn.Extensions;

internal static class IocRegistrations
{
    public static IServiceCollection AddAllOfType(this IServiceCollection services, Type type)
    {
        var assembly = Assembly.GetExecutingAssembly();

        var handlers = assembly.GetTypes()
            .Where(t => (!type.IsGenericType && t.GetInterfaces().Any(x => x == type)) ||
                        t.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == type))
            .ToList();

        foreach (var handler in handlers)
        {
            var interfaceType = handler.GetInterfaces()
                .First(i => !i.IsGenericType || (i.IsGenericType && i.GetGenericTypeDefinition() == type));
            services.AddTransient(interfaceType, handler);
        }

        return services;
    }

    public static IServiceCollection AddAllOfType<T>(this IServiceCollection services)
    {
        return services.AddAllOfType(typeof(T));
    }

    public static IServiceCollection AddRepositories(this IServiceCollection services)
    {
        return services
            .AddScoped<IDbRepository<Account>, AccountRepository>()
            // Register CharacterRepository with caching wrapper
            .AddScoped<IDbRepository<Character>>(sp =>
            {
                var context = sp.GetRequiredService<AcornDbContext>();
                var cache = sp.GetRequiredService<ICacheService>();
                var logger = sp.GetRequiredService<ILogger<CharacterRepository>>();
                var cachedLogger = sp.GetRequiredService<ILogger<CachedCharacterRepository>>();

                var inner = new CharacterRepository(context, logger);
                return new CachedCharacterRepository(inner, cache, cachedLogger);
            })
            .AddSingleton<IDataFileRepository, DataFileRepository>()
            .AddSingleton<IShopDataRepository, ShopDataRepository>()
            .AddSingleton<ISkillMasterDataRepository, SkillMasterDataRepository>()
            .AddSingleton<IInnDataRepository, InnDataRepository>()
            .AddSingleton<IQuestDataRepository, QuestDataRepository>()
            .AddScoped<IBoardRepository, BoardRepository>();
    }

    public static IServiceCollection AddWorldServices(this IServiceCollection services)
    {
        return services
            .AddSingleton<IFormulaService, FormulaService>()
            .AddSingleton<ILootService, LootService>()
            .AddSingleton<IMapTileService, MapTileService>()
            .AddSingleton<IMapBroadcastService, MapBroadcastService>()
            .AddSingleton<INpcCombatService, NpcCombatService>()
            .AddSingleton<IPlayerController, PlayerController>()
            .AddSingleton<INpcController, NpcController>()
            .AddSingleton<IMapController, MapController>()
            .AddSingleton<IMapEffectService, MapEffectService>()
            .AddSingleton<IArenaService, ArenaService>()
            .AddSingleton<IPartyService, PartyService>()
            .AddSingleton<IGuildService, GuildService>()
            .AddSingleton<IQuestService, QuestService>()
            .AddSingleton<IAdminService, AdminService>()
            .AddSingleton<IMarriageService, MarriageService>()
            .AddSingleton<IMapItemService, MapItemService>()
            // Lazy<T> registration to break circular dependencies
            .AddTransient(typeof(Lazy<>), typeof(LazyServiceProvider<>));
    }

    /// <summary>
    /// Helper class for Lazy&lt;T&gt; resolution to break circular dependencies
    /// </summary>
    private class LazyServiceProvider<T> : Lazy<T> where T : class
    {
        public LazyServiceProvider(IServiceProvider serviceProvider)
            : base(() => serviceProvider.GetRequiredService<T>())
        {
        }
    }

    /// <summary>
    /// Auto-discovers and registers all IPacketHandler&lt;T&gt; implementations in the assembly.
    /// Each handler is registered as both its typed interface and the marker IPacketHandler interface.
    /// </summary>
    public static IServiceCollection AddPacketHandlers(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var handlerTypes = assembly.GetTypes()
            .Where(t => t is { IsAbstract: false, IsInterface: false })
            .Where(t => t.GetInterfaces().Any(i =>
                i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IPacketHandler<>)))
            .ToList();

        foreach (var handlerType in handlerTypes)
        {
            var packetInterface = handlerType.GetInterfaces()
                .First(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IPacketHandler<>));

            // Register as IPacketHandler<TPacket>
            services.AddTransient(packetInterface, handlerType);

            // Register as IPacketHandler (marker) by resolving via the typed interface
            var capturedInterface = packetInterface;
            services.AddTransient<IPacketHandler>(sp =>
                (IPacketHandler)sp.GetRequiredService(capturedInterface));
        }

        return services;
    }
}

public delegate DateTime UtcNowDelegate();