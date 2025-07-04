using System.Reflection;
using Acorn.Database.Models;
using Acorn.Database.Repository;
using Acorn.Net.PacketHandlers;
using Acorn.Net.PacketHandlers.Account;
using Acorn.Net.PacketHandlers.Character;
using Acorn.Net.PacketHandlers.Npc;
using Acorn.Net.PacketHandlers.Player;
using Acorn.Net.PacketHandlers.Player.Talk;
using Acorn.Net.PacketHandlers.Player.Warp;
using Microsoft.Extensions.DependencyInjection;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;

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
            .AddSingleton<IDbRepository<Account>, AccountRepository>()
            .AddSingleton<IDbRepository<Database.Models.Character>, CharacterRepository>()
            .AddSingleton<IDataFileRepository, DataFileRepository>();
    }

    public static IServiceCollection AddPacketHandlers(this IServiceCollection services)
    {
        void AddPacketHandler<TPacket, THandler>()
            where TPacket : IPacket
            where THandler : class, IPacketHandler<TPacket>, IPacketHandler
        {
            services.AddTransient<IPacketHandler<TPacket>, THandler>();
            services.AddTransient<IPacketHandler, THandler>(sp => (THandler)sp.GetRequiredService<IPacketHandler<TPacket>>());
        }

        AddPacketHandler<AccountCreateClientPacket, AccountCreateClientPacketHandler>();
        AddPacketHandler<AccountRequestClientPacket, AccountRequestClientPacketHandler>();
        AddPacketHandler<LoginRequestClientPacket, LoginRequestClientPacketHandler>();
        AddPacketHandler<CharacterCreateClientPacket, CharacterCreateClientPacketHandler>();
        AddPacketHandler<CharacterRequestClientPacket, CharacterRequestClientPacketHandler>();
        AddPacketHandler<NpcRangeRequestClientPacket, NpcRangeRequestClientPacketHandler>();
        AddPacketHandler<WarpAcceptClientPacket, WarpAcceptClientPacketHandler>();
        AddPacketHandler<WarpTakeClientPacket, WarpTakeClientPacketHandler>();
        AddPacketHandler<TalkAnnounceClientPacket, TalkAnnounceClientPacketHandler>();
        AddPacketHandler<TalkMsgClientPacket, TalkMsgClientPacketHandler>();
        AddPacketHandler<TalkReportClientPacket, TalkReportClientPacketHandler>();
        AddPacketHandler<AttackUseClientPacket, AttackUseClientPacketHandler>();
        AddPacketHandler<ConnectionAcceptClientPacket, ConnectionAcceptClientPacketHandler>();
        AddPacketHandler<ConnectionPingClientPacket, ConnectionPingClientPacketHandler>();
        AddPacketHandler<DoorOpenClientPacket, DoorOpenClientPacketHandler>();
        AddPacketHandler<GlobalCloseClientPacket, GlobalCloseClientPacketHandler>();
        AddPacketHandler<GlobalOpenClientPacket, GlobalOpenClientPacketHandler>();
        AddPacketHandler<InitInitClientPacket, InitInitClientPacketHandler>();
        AddPacketHandler<PaperdollRequestClientPacket, PaperdollRequestClientPacketHandler>();
        AddPacketHandler<PlayerRangeRequestClientPacket, PlayerRangeRequestClientPacketHandler>();
        AddPacketHandler<PlayersRequestClientPacket, PlayersRequestClientPacketHandler>();
        AddPacketHandler<RefreshRequestClientPacket, RefreshRequestClientPacketHandler>();
        AddPacketHandler<WalkAdminClientPacket, WalkAdminClientPacketHandler>();
        AddPacketHandler<WalkPlayerClientPacket, WalkPlayerClientPacketHandler>();
        AddPacketHandler<WelcomeAgreeClientPacket, WelcomeAgreeClientPacketHandler>();
        AddPacketHandler<WelcomeMsgClientPacket, WelcomeMsgClientPacketHandler>();
        AddPacketHandler<WelcomeRequestClientPacket, WelcomeRequestClientPacketHandler>();
        AddPacketHandler<FacePlayerClientPacket, FacePlayerClientPacketHandler>();
        return services;
    }
}

public delegate DateTime UtcNowDelegate();