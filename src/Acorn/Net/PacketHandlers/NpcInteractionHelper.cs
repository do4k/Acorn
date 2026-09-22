using Acorn.Extensions;
using Acorn.World.Npc;
using Acorn.World.Services.Map;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol.Pub;
using NpcType = Moffat.EndlessOnline.SDK.Protocol.Pub.NpcType;

namespace Acorn.Net.PacketHandlers;

public static class NpcInteractionHelper
{
    /// <summary>
    /// Validates that the NPC exists on the player's map, is of the expected type,
    /// is within the player's client view, and sets the player's InteractingNpcIndex.
    /// Returns the NPC state if valid, null otherwise.
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

        if (!IsInPlayerView(player, npc))
        {
            LogOutOfView(logger, player, npcIndex, npc);
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

        if (!IsInPlayerView(player, npc))
        {
            LogOutOfView(logger, player, npcIndex, npc);
            return null;
        }

        return npc;
    }

    /// <summary>
    ///     An interaction is only valid while the NPC is inside the player's client view -
    ///     the same range the server uses to decide which NPCs to send to the client. Both
    ///     the native and web clients send shop/bank/barber/trainer packets the moment the
    ///     NPC sprite is clicked, from wherever the player happens to stand, so anything a
    ///     real player can click passes this check. Crafted packets referencing NPCs outside
    ///     the player's view are rejected. Cross-map spoofing is already blocked by the
    ///     per-map index lookup in the callers.
    /// </summary>
    private static bool IsInPlayerView(PlayerState player, NpcState npc)
    {
        if (player.Character is null) return false;

        return MapTileService.IsInClientView(player.Character.AsCoords(), npc.AsCoords());
    }

    private static void LogOutOfView(ILogger logger, PlayerState player, int npcIndex, NpcState npc)
    {
        logger.LogWarning(
            "Player {Character} at ({PlayerX}, {PlayerY}) tried to interact with NPC {NpcIndex} at ({NpcX}, {NpcY}) from outside their view",
            player.Character?.Name, player.Character?.X, player.Character?.Y, npcIndex, npc.X, npc.Y);
    }
}
