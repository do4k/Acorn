using Acorn.Database;
using Acorn.Game.Services;
using Acorn.Net;
using Acorn.Options;
using Acorn.World.Map;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moffat.EndlessOnline.SDK.Protocol;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;
using Moffat.EndlessOnline.SDK.Protocol.Pub;

namespace Acorn.World.Services.Marriage;

public class MarriageService(
    IWorldQueries world,
    IInventoryService inventoryService,
    IServiceScopeFactory scopeFactory,
    IOptions<MarriageOptions> marriageOptions,
    ILogger<MarriageService> logger) : IMarriageService
{
    private const int GoldItemId = 1;
    private readonly MarriageOptions _options = marriageOptions.Value;

    public async Task OpenPriestAsync(PlayerState player, int npcIndex)
    {
        var character = player.Character!;
        var map = player.CurrentMap!;

        // Must have a fiance and not already be married
        if (string.IsNullOrEmpty(character.Fiance) || !string.IsNullOrEmpty(character.Partner))
        {
            return;
        }

        // Validate NPC
        if (!map.Npcs.TryGetValue(npcIndex, out var npc) || npc.Data.Type != NpcType.Priest)
        {
            return;
        }

        // Check if a wedding is already in progress
        if (map.Wedding != null
            && map.Wedding.State != WeddingState.Requested
            && map.Wedding.State != WeddingState.Done)
        {
            await player.Send(new PriestReplyServerPacket
            {
                ReplyCode = PriestReply.Busy
            });
            return;
        }

        // Check level
        if (character.Level < _options.MinLevel)
        {
            await player.Send(new PriestReplyServerPacket
            {
                ReplyCode = PriestReply.LowLevel
            });
            return;
        }

        // Check wedding attire
        if (!IsDressedForWedding(character))
        {
            await player.Send(new PriestReplyServerPacket
            {
                ReplyCode = PriestReply.NotDressed
            });
            return;
        }

        player.InteractingNpcIndex = npcIndex;
        await player.Send(new PriestOpenServerPacket
        {
            SessionId = player.SessionId
        });
    }

    public async Task RequestWeddingAsync(PlayerState player, string partnerName)
    {
        var character = player.Character!;
        var map = player.CurrentMap!;
        var npcIndex = player.InteractingNpcIndex;

        if (npcIndex == null)
        {
            return;
        }

        // Validate priest NPC
        if (!map.Npcs.TryGetValue(npcIndex.Value, out var npc) || npc.Data.Type != NpcType.Priest)
        {
            return;
        }

        // Check fiance matches requested name
        if (string.IsNullOrEmpty(character.Fiance)
            || !character.Fiance.Equals(partnerName, StringComparison.OrdinalIgnoreCase))
        {
            await player.Send(new PriestReplyServerPacket
            {
                ReplyCode = PriestReply.NoPermission
            });
            return;
        }

        // Find partner on the same map
        var partner = map.Players.Values.FirstOrDefault(p =>
            p.Character?.Name?.Equals(partnerName, StringComparison.OrdinalIgnoreCase) == true);

        if (partner?.Character == null)
        {
            await player.Send(new PriestReplyServerPacket
            {
                ReplyCode = PriestReply.PartnerNotPresent
            });
            return;
        }

        // Partner must not already be married
        if (!string.IsNullOrEmpty(partner.Character.Partner))
        {
            await player.Send(new PriestReplyServerPacket
            {
                ReplyCode = PriestReply.PartnerAlreadyMarried
            });
            return;
        }

        // Partner's fiance must be this player
        if (string.IsNullOrEmpty(partner.Character.Fiance)
            || !partner.Character.Fiance.Equals(character.Name, StringComparison.OrdinalIgnoreCase))
        {
            await player.Send(new PriestReplyServerPacket
            {
                ReplyCode = PriestReply.NoPermission
            });
            return;
        }

        // Partner must be dressed for wedding
        if (!IsDressedForWedding(partner.Character))
        {
            await player.Send(new PriestReplyServerPacket
            {
                ReplyCode = PriestReply.PartnerNotDressed
            });
            return;
        }

        // Start the wedding
        map.Wedding = new Wedding
        {
            PlayerSessionId = player.SessionId,
            PartnerSessionId = partner.SessionId,
            NpcIndex = npcIndex.Value,
            State = WeddingState.Requested
        };
        map.WeddingTicks = 0;

        logger.LogInformation("Wedding requested: {Player} and {Partner} on map {MapId}",
            character.Name, partner.Character.Name, map.Id);

        // Send request to partner
        await partner.Send(new PriestRequestServerPacket
        {
            SessionId = partner.SessionId,
            PartnerName = character.Name!
        });
    }

    public Task AcceptWeddingRequestAsync(PlayerState player)
    {
        var map = player.CurrentMap!;
        var wedding = map.Wedding;

        if (wedding == null
            || wedding.PartnerSessionId != player.SessionId
            || wedding.State != WeddingState.Requested)
        {
            return Task.CompletedTask;
        }

        wedding.State = WeddingState.Accepted;
        map.WeddingTicks = 0;

        logger.LogInformation("Wedding accepted by {Player} on map {MapId}",
            player.Character!.Name, map.Id);

        return Task.CompletedTask;
    }

    public Task SayIDoAsync(PlayerState player)
    {
        var map = player.CurrentMap!;
        var wedding = map.Wedding;

        if (wedding == null)
        {
            return Task.CompletedTask;
        }

        if (wedding.PartnerSessionId == player.SessionId && wedding.State == WeddingState.WaitingForPartner)
        {
            wedding.State = WeddingState.PartnerAgrees;
            map.WeddingTicks = 0;
        }

        if (wedding.PlayerSessionId == player.SessionId && wedding.State == WeddingState.WaitingForPlayer)
        {
            wedding.State = WeddingState.PlayerAgrees;
            map.WeddingTicks = 0;
        }

        return Task.CompletedTask;
    }

    public async Task OpenLawAsync(PlayerState player, int npcIndex)
    {
        var map = player.CurrentMap!;

        // Validate NPC
        if (!map.Npcs.TryGetValue(npcIndex, out var npc) || npc.Data.Type != NpcType.Lawyer)
        {
            return;
        }

        player.InteractingNpcIndex = npcIndex;
        await player.Send(new MarriageOpenServerPacket
        {
            SessionId = player.SessionId
        });
    }

    public async Task RequestMarriageApprovalAsync(PlayerState player, string partnerName)
    {
        var character = player.Character!;
        var map = player.CurrentMap!;
        var npcIndex = player.InteractingNpcIndex;

        if (npcIndex == null)
        {
            return;
        }

        // Validate lawyer NPC
        if (!map.Npcs.TryGetValue(npcIndex.Value, out var npc) || npc.Data.Type != NpcType.Lawyer)
        {
            return;
        }

        // Can't get approval if already married
        if (!string.IsNullOrEmpty(character.Partner))
        {
            await player.Send(new MarriageReplyServerPacket
            {
                ReplyCode = MarriageReply.AlreadyMarried
            });
            return;
        }

        // Check gold
        if (inventoryService.GetItemAmount(character, GoldItemId) < _options.ApprovalCost)
        {
            await player.Send(new MarriageReplyServerPacket
            {
                ReplyCode = MarriageReply.NotEnoughGold
            });
            return;
        }

        // Deduct gold and set fiance
        inventoryService.TryRemoveItem(character, GoldItemId, _options.ApprovalCost);
        character.Fiance = partnerName;

        logger.LogInformation("Marriage approval: {Player} is now engaged to {Partner}",
            character.Name, partnerName);

        await player.Send(new MarriageReplyServerPacket
        {
            ReplyCode = MarriageReply.Success,
            ReplyCodeData = new MarriageReplyServerPacket.ReplyCodeDataSuccess
            {
                GoldAmount = inventoryService.GetItemAmount(character, GoldItemId)
            }
        });
    }

    public async Task RequestDivorceAsync(PlayerState player, string partnerName)
    {
        var character = player.Character!;
        var map = player.CurrentMap!;
        var npcIndex = player.InteractingNpcIndex;

        if (npcIndex == null)
        {
            return;
        }

        // Validate lawyer NPC
        if (!map.Npcs.TryGetValue(npcIndex.Value, out var npc) || npc.Data.Type != NpcType.Lawyer)
        {
            return;
        }

        // Must be married
        if (string.IsNullOrEmpty(character.Partner))
        {
            await player.Send(new MarriageReplyServerPacket
            {
                ReplyCode = MarriageReply.NotMarried
            });
            return;
        }

        // Name must match partner
        if (!character.Partner.Equals(partnerName, StringComparison.OrdinalIgnoreCase))
        {
            await player.Send(new MarriageReplyServerPacket
            {
                ReplyCode = MarriageReply.WrongName
            });
            return;
        }

        // Check gold
        if (inventoryService.GetItemAmount(character, GoldItemId) < _options.DivorceCost)
        {
            await player.Send(new MarriageReplyServerPacket
            {
                ReplyCode = MarriageReply.NotEnoughGold
            });
            return;
        }

        // Process divorce
        inventoryService.TryRemoveItem(character, GoldItemId, _options.DivorceCost);
        character.Partner = null;

        logger.LogInformation("Divorce: {Player} divorced {Partner}", character.Name, partnerName);

        await player.Send(new MarriageReplyServerPacket
        {
            ReplyCode = MarriageReply.Success,
            ReplyCodeData = new MarriageReplyServerPacket.ReplyCodeDataSuccess
            {
                GoldAmount = inventoryService.GetItemAmount(character, GoldItemId)
            }
        });

        // Divorce the partner if they're online
        var onlinePartner = world.FindPlayerByName(partnerName);
        if (onlinePartner?.Character != null)
        {
            onlinePartner.Character.Partner = null;
            await onlinePartner.Send(new MarriageReplyServerPacket
            {
                ReplyCode = MarriageReply.DivorceNotification
            });
            return;
        }

        // Partner is offline - update via database
        await DivorceOfflinePartnerAsync(partnerName);
    }

    public async Task ProcessWeddingTickAsync(MapState map)
    {
        var wedding = map.Wedding;
        if (wedding == null || wedding.State == WeddingState.Requested)
        {
            return;
        }

        var waitFor = wedding.State switch
        {
            WeddingState.Accepted or WeddingState.PlayerAgrees or WeddingState.PartnerAgrees => 0,
            WeddingState.PriestDialog5AndConfetti => 2,
            WeddingState.PriestDialog1 => _options.CeremonyStartDelaySeconds,
            WeddingState.AskPlayer or WeddingState.AskPartner or WeddingState.PriestDialog3 => 3,
            WeddingState.WaitingForPlayer or WeddingState.WaitingForPartner => 20,
            _ => 9
        };

        map.WeddingTicks++;

        if (map.WeddingTicks < waitFor)
        {
            return;
        }

        var playerState = map.Players.Values.FirstOrDefault(p => p.SessionId == wedding.PlayerSessionId);
        var partnerState = map.Players.Values.FirstOrDefault(p => p.SessionId == wedding.PartnerSessionId);

        // If either player left, cancel the wedding
        if (playerState?.Character == null || partnerState?.Character == null)
        {
            await NpcChatAsync(map, wedding.NpcIndex, "The wedding has been cancelled.");
            map.Wedding = null;
            map.WeddingTicks = 0;
            return;
        }

        var playerName = playerState.Character.Name!;
        var partnerName = partnerState.Character.Name!;

        var nextState = wedding.State switch
        {
            WeddingState.Accepted => await HandleAcceptedAsync(map, wedding),
            WeddingState.PriestDialog1 => await HandleDialog1Async(map, wedding, playerName, partnerName),
            WeddingState.PriestDialog2 => await HandleDialog2Async(map, wedding, playerName, partnerName),
            WeddingState.PriestDoYouPartner => await HandleDoYouPartnerAsync(map, wedding, playerName, partnerName),
            WeddingState.AskPartner => await HandleAskPartnerAsync(partnerState),
            WeddingState.WaitingForPartner or WeddingState.WaitingForPlayer => await HandleTimeoutAsync(map, wedding),
            WeddingState.PartnerAgrees => await HandlePartnerAgreesAsync(map, wedding),
            WeddingState.PriestDoYouPlayer => await HandleDoYouPlayerAsync(map, wedding, playerName, partnerName),
            WeddingState.AskPlayer => await HandleAskPlayerAsync(playerState),
            WeddingState.PlayerAgrees => await HandlePlayerAgreesAsync(map, wedding),
            WeddingState.PriestDialog3 => await HandleDialog3Async(map, wedding, playerState, partnerState),
            WeddingState.PriestDialog4 => await HandleDialog4Async(map, wedding),
            WeddingState.Hearts => await HandleHeartsAsync(map, wedding),
            WeddingState.PriestDialog5AndConfetti => await HandleDialog5Async(map, wedding, playerName, partnerName),
            WeddingState.Done => await HandleDoneAsync(map),
            _ => (WeddingState?)null
        };

        if (nextState.HasValue && map.Wedding != null)
        {
            map.WeddingTicks = 0;
            map.Wedding.State = nextState.Value;
        }
    }

    private async Task<WeddingState> HandleAcceptedAsync(MapState map, Wedding wedding)
    {
        await NpcChatAsync(map, wedding.NpcIndex,
            $"The wedding ceremony will begin in {_options.CeremonyStartDelaySeconds} seconds.");

        // Play music
        await map.BroadcastPacket(new JukeboxPlayerServerPacket
        {
            MfxId = _options.MfxId
        });

        return WeddingState.PriestDialog1;
    }

    private async Task<WeddingState> HandleDialog1Async(MapState map, Wedding wedding, string playerName, string partnerName)
    {
        await NpcChatAsync(map, wedding.NpcIndex,
            $"Dearly beloved, we are gathered here today to witness the union of {partnerName} and {playerName}.");
        return WeddingState.PriestDialog2;
    }

    private async Task<WeddingState> HandleDialog2Async(MapState map, Wedding wedding, string playerName, string partnerName)
    {
        await NpcChatAsync(map, wedding.NpcIndex,
            $"If anyone objects to the marriage of {partnerName} and {playerName}, speak now or forever hold your peace.");
        return WeddingState.PriestDoYouPartner;
    }

    private async Task<WeddingState> HandleDoYouPartnerAsync(MapState map, Wedding wedding, string playerName, string partnerName)
    {
        await NpcChatAsync(map, wedding.NpcIndex,
            $"Do you, {partnerName}, take {playerName} to be your lawfully wedded partner?");
        return WeddingState.AskPartner;
    }

    private async Task<WeddingState> HandleAskPartnerAsync(PlayerState partnerState)
    {
        await partnerState.Send(new PriestReplyServerPacket
        {
            ReplyCode = PriestReply.DoYou
        });
        return WeddingState.WaitingForPartner;
    }

    private async Task<WeddingState?> HandleTimeoutAsync(MapState map, Wedding wedding)
    {
        await NpcChatAsync(map, wedding.NpcIndex, "The wedding has been cancelled.");
        map.Wedding = null;
        map.WeddingTicks = 0;
        return null;
    }

    private async Task<WeddingState> HandlePartnerAgreesAsync(MapState map, Wedding wedding)
    {
        await PlayerChatAsync(map, wedding.PartnerSessionId, "I do!");
        return WeddingState.PriestDoYouPlayer;
    }

    private async Task<WeddingState> HandleDoYouPlayerAsync(MapState map, Wedding wedding, string playerName, string partnerName)
    {
        await NpcChatAsync(map, wedding.NpcIndex,
            $"Do you, {playerName}, take {partnerName} to be your lawfully wedded partner?");
        return WeddingState.AskPlayer;
    }

    private async Task<WeddingState> HandleAskPlayerAsync(PlayerState playerState)
    {
        await playerState.Send(new PriestReplyServerPacket
        {
            ReplyCode = PriestReply.DoYou
        });
        return WeddingState.WaitingForPlayer;
    }

    private async Task<WeddingState> HandlePlayerAgreesAsync(MapState map, Wedding wedding)
    {
        await PlayerChatAsync(map, wedding.PlayerSessionId, "I do!");
        return WeddingState.PriestDialog3;
    }

    private async Task<WeddingState> HandleDialog3Async(MapState map, Wedding wedding,
        PlayerState playerState, PlayerState partnerState)
    {
        await NpcChatAsync(map, wedding.NpcIndex,
            "Then by the power vested in me, I now pronounce you married!");

        // Give rings
        inventoryService.TryAddItem(playerState.Character!, _options.RingItemId);
        inventoryService.TryAddItem(partnerState.Character!, _options.RingItemId);

        // Set partners
        var playerName = playerState.Character!.Name!;
        var partnerName = partnerState.Character!.Name!;

        playerState.Character.Partner = partnerName;
        playerState.Character.Fiance = null;
        partnerState.Character.Partner = playerName;
        partnerState.Character.Fiance = null;

        logger.LogInformation("Wedding complete: {Player} and {Partner} are now married on map {MapId}",
            playerName, partnerName, map.Id);

        return WeddingState.PriestDialog4;
    }

    private async Task<WeddingState> HandleDialog4Async(MapState map, Wedding wedding)
    {
        await NpcChatAsync(map, wedding.NpcIndex, "Congratulations to the happy couple!");
        return WeddingState.Hearts;
    }

    private async Task<WeddingState> HandleHeartsAsync(MapState map, Wedding wedding)
    {
        // Send celebration effect to all players on map
        await map.BroadcastPacket(new EffectPlayerServerPacket
        {
            Effects =
            [
                new PlayerEffect { PlayerId = wedding.PlayerSessionId, EffectId = _options.CelebrationEffectId },
                new PlayerEffect { PlayerId = wedding.PartnerSessionId, EffectId = _options.CelebrationEffectId }
            ]
        });
        return WeddingState.PriestDialog5AndConfetti;
    }

    private async Task<WeddingState> HandleDialog5Async(MapState map, Wedding wedding, string playerName, string partnerName)
    {
        await NpcChatAsync(map, wedding.NpcIndex,
            $"May the love between {partnerName} and {playerName} last for all eternity!");

        // Confetti effect (effect ID 11)
        await map.BroadcastPacket(new EffectPlayerServerPacket
        {
            Effects =
            [
                new PlayerEffect { PlayerId = wedding.PlayerSessionId, EffectId = 11 },
                new PlayerEffect { PlayerId = wedding.PartnerSessionId, EffectId = 11 }
            ]
        });

        return WeddingState.Done;
    }

    private async Task<WeddingState?> HandleDoneAsync(MapState map)
    {
        await NpcChatAsync(map, map.Wedding!.NpcIndex, "You may now kiss!");
        map.Wedding = null;
        map.WeddingTicks = 0;
        return null;
    }

    private async Task NpcChatAsync(MapState map, int npcIndex, string message)
    {
        await map.BroadcastPacket(new NpcPlayerServerPacket
        {
            Chats = [new NpcUpdateChat { NpcIndex = npcIndex, Message = message }]
        });
    }

    private async Task PlayerChatAsync(MapState map, int sessionId, string message)
    {
        await map.BroadcastPacket(new TalkPlayerServerPacket
        {
            PlayerId = sessionId,
            Message = message
        });
    }

    private bool IsDressedForWedding(Game.Models.Character character)
    {
        return character.Gender switch
        {
            Gender.Female => character.Paperdoll.Armor == _options.FemaleArmorId,
            Gender.Male => character.Paperdoll.Armor == _options.MaleArmorId,
            _ => false
        };
    }

    private async Task DivorceOfflinePartnerAsync(string partnerName)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AcornDbContext>();

            var dbCharacter = await dbContext.Characters
                .FirstOrDefaultAsync(c => c.Name == partnerName);

            if (dbCharacter != null)
            {
                dbCharacter.Partner = null;
                await dbContext.SaveChangesAsync();
                logger.LogInformation("Divorced offline partner {Partner} via database", partnerName);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to divorce offline partner {Partner}", partnerName);
        }
    }
}
