using Moffat.EndlessOnline.SDK.Protocol;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;
using Moffat.EndlessOnline.SDK.Protocol.Pub;

namespace Acorn.World.Npc;

/// <summary>
///     Tracks a player who has attacked an NPC
/// </summary>
public class NpcOpponent
{
    public int PlayerId { get; set; }
    public int DamageDealt { get; set; }
    public int BoredTicks { get; set; }
}
