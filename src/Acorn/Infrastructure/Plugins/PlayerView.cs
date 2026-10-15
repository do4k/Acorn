using Acorn.Net;
using Acorn.Plugins;

namespace Acorn.Infrastructure.Plugins;

/// <summary>
///     Thin facade over the live <see cref="PlayerState" />: reads reflect
///     current server state, internals stay hidden from plugins.
/// </summary>
internal sealed class PlayerView(PlayerState player) : IPlayerView
{
    /// <summary>The wrapped session state. Host-internal; never crosses the plugin boundary.</summary>
    internal PlayerState Player { get; } = player;

    public int SessionId => Player.SessionId;

    public string? Name => Player.Character?.Name;

    public int MapId => Player.Character?.Map ?? 0;

    public int X => Player.Character?.X ?? 0;

    public int Y => Player.Character?.Y ?? 0;

    public int Level => Player.Character?.Level ?? 0;

    public int Hp => Player.Character?.Hp ?? 0;

    public int MaxHp => Player.Character?.MaxHp ?? 0;
}
