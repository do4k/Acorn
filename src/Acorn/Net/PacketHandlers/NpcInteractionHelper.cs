using Acorn.World.Npc;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol.Pub;
using NpcType = Moffat.EndlessOnline.SDK.Protocol.Pub.NpcType;

namespace Acorn.Net.PacketHandlers;

public static class NpcInteractionHelper
{
    /// <summary>
    /// Validates that the NPC exists on the player's map, is of the expected type,
    /// and sets the player's InteractingNpcIndex. Returns the NPC state if valid, null otherwise.
    /// </summary>
    public static NpcState? ValidateAndStartInteraction(
        PlayerState player, int npcIndex, NpcType expectedType, ILogger logger)
    {
        if (player.CurrentMap is null) return null;

        if (!player.CurrentMap.Npcs.TryGetValue(npcIndex, out var npc))
        {
            logger.LogWarning("Player {Character} tried to interact with invalid NPC index {NpcIndex}",
                player.Character?.Name, npcIndex);
            return null;
        }

        if (npc.Data.Type != expectedType)
        {
            logger.LogWarning("Player {Character} tried to interact with NPC {NpcIndex} but it is not type {ExpectedType}",
                player.Character?.Name, npcIndex, expectedType);
            return null;
        }

        player.InteractingNpcIndex = npcIndex;
        return npc;
    }

    /// <summary>
    /// Validates that the player is currently interacting with an NPC of the expected type.
    /// Does not set InteractingNpcIndex (it should already be set from the Open handler).
    /// Returns the NPC state if valid, null otherwise.
    /// </summary>
    public static NpcState? ValidateInteraction(
        PlayerState player, NpcType expectedType, ILogger logger)
    {
        if (player.InteractingNpcIndex is null)
        {
            logger.LogWarning("Player {Character} attempted action without interacting with NPC",
                player.Character?.Name);
            return null;
        }

        if (player.CurrentMap is null) return null;

        var npcIndex = player.InteractingNpcIndex.Value;
        if (!player.CurrentMap.Npcs.TryGetValue(npcIndex, out var npc))
        {
            logger.LogWarning("Player {Character} tried to interact with invalid NPC index {NpcIndex}",
                player.Character?.Name, npcIndex);
            return null;
        }

        if (npc.Data.Type != expectedType)
        {
            logger.LogWarning("Player {Character} tried to interact with NPC {NpcIndex} but it is not type {ExpectedType}",
                player.Character?.Name, npcIndex, expectedType);
            return null;
        }

        return npc;
    }
}
