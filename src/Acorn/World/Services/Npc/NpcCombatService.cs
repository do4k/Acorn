using Acorn.Net;
using Acorn.World.Npc;
using Moffat.EndlessOnline.SDK.Protocol;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;
using SdkNpcType = Moffat.EndlessOnline.SDK.Protocol.Pub.NpcType;

namespace Acorn.World.Services.Npc;

public class NpcCombatService : INpcCombatService
{
    public void AddOpponent(NpcState npc, int playerId, int damage)
    {
        npc.AddOpponent(playerId, damage);
    }

    public void ProcessOpponentBoredom(NpcState npc, int incrementTicks, int boredThreshold)
    {
        npc.IncrementBoredTicks(incrementTicks);
        npc.DropBoredOpponents(boredThreshold);
    }

    public int GetActRate(int spawnType)
    {
        return spawnType switch
        {
            0 => 4, // Fastest
            1 => 8,
            2 => 12,
            3 => 16,
            4 => 24,
            5 => 32,
            6 => 48, // Slowest
            7 => 0, // Stationary - never acts
            _ => 16
        };
    }

    public NpcUpdateAttack? TryAttack(NpcState npc, int npcIndex, IEnumerable<PlayerState> players,
        IFormulaService formulaService)
    {
        // Only aggressive/passive NPCs can attack
        if (npc.Data.Type != SdkNpcType.Aggressive && npc.Data.Type != SdkNpcType.Passive)
        {
            return null;
        }

        // Passive NPCs only attack if they have opponents
        if (npc.Data.Type == SdkNpcType.Passive && npc.Opponents.Count == 0)
        {
            return null;
        }

        // Find adjacent tiles

        // Find adjacent tiles
        var adjacentCoords = new[]
        {
            new Coords { X = npc.X, Y = npc.Y - 1 }, // Up
            new Coords { X = npc.X, Y = npc.Y + 1 }, // Down  
            new Coords { X = npc.X - 1, Y = npc.Y }, // Left
            new Coords { X = npc.X + 1, Y = npc.Y } // Right
        };

        // Find players on adjacent tiles
        var playerList = players.ToList();
        var adjacentPlayers = playerList
            .Where(p => p.Character != null &&
                        adjacentCoords.Any(c => c.X == p.Character.X && c.Y == p.Character.Y))
            .ToList();

        if (adjacentPlayers.Count == 0)
        {
            return null;
        }

        // Prioritize opponents who have attacked this NPC
        PlayerState? target = null;

        // First check for adjacent opponents (sorted by damage dealt)
        var adjacentOpponent = npc.Opponents
            .Where(o => adjacentPlayers.Any(p => p.SessionId == o.PlayerId))
            .MaxBy(o => o.DamageDealt);

        if (adjacentOpponent != null)
        {
            target = adjacentPlayers.FirstOrDefault(p => p.SessionId == adjacentOpponent.PlayerId);
        }

        // If no adjacent opponent, aggressive NPCs attack random adjacent player
        if (target == null && npc.Data.Type == SdkNpcType.Aggressive)
        {
            target = adjacentPlayers[Random.Shared.Next(adjacentPlayers.Count)];
        }

        if (target?.Character == null)
        {
            return null;
        }

        // Calculate direction to face target
        var xDiff = npc.X - target.Character.X;
        var yDiff = npc.Y - target.Character.Y;

        var direction = (xDiff, yDiff) switch
        {
            (0, 1) => Direction.Up,
            (0, -1) => Direction.Down,
            (1, 0) => Direction.Left,
            (-1, 0) => Direction.Right,
            _ => npc.Direction
        };

        npc.Direction = direction;

        // Check if NPC is attacking from behind or side (critical hit)
        var directionDiff = Math.Abs((int)target.Character.Direction - (int)npc.Direction);
        var attackingBackOrSide = directionDiff != 2;

        // Calculate damage
        var damage = formulaService.CalculateNpcDamageToPlayer(npc.Data, target.Character, attackingBackOrSide);
        target.Character.Hp -= damage;
        target.Character.Hp = Math.Max(0, target.Character.Hp);

        var hpPercentage = target.Character.MaxHp > 0
            ? (int)((double)target.Character.Hp / target.Character.MaxHp * 100)
            : 0;

        var killed = target.Character.Hp == 0
            ? PlayerKilledState.Killed
            : PlayerKilledState.Alive;

        // If player died, remove them from opponents
        if (killed == PlayerKilledState.Killed)
        {
            npc.Opponents.RemoveAll(o => o.PlayerId == target.SessionId);
        }

        return new NpcUpdateAttack
        {
            NpcIndex = npcIndex,
            Killed = killed,
            Direction = direction,
            PlayerId = target.SessionId,
            Damage = damage,
            HpPercentage = hpPercentage
        };
    }
}