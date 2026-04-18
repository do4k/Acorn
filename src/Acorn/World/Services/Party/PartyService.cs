using System.Collections.Concurrent;
using Acorn.Net;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;

namespace Acorn.World.Services.Party;

/// <summary>
/// Manages all party state and operations.
/// </summary>
public class PartyService(
    WorldState worldState,
    ILogger<PartyService> logger) : IPartyService
{
    private const int MaxPartySize = 9;

    private readonly ConcurrentBag<Party> _parties = [];

    // Track pending party requests: targetSessionId -> (requesterSessionId, requestType)
    private readonly ConcurrentDictionary<int, (int RequesterSessionId, PartyRequestType Type)> _pendingRequests = [];

    public Party? GetPlayerParty(int sessionId)
    {
        return _parties.FirstOrDefault(p => p.ContainsMember(sessionId));
    }

    public async Task RequestParty(PlayerState requester, int targetSessionId, PartyRequestType type)
    {
        if (requester.Character is null || requester.CurrentMap is null)
        {
            return;
        }

        var target = requester.CurrentMap.Players.Values.FirstOrDefault(p => p.SessionId == targetSessionId);
        if (target?.Character is null)
        {
            return;
        }

        // Check if target is already in a party
        var targetParty = GetPlayerParty(targetSessionId);
        var requesterParty = GetPlayerParty(requester.SessionId);

        if (type == PartyRequestType.Invite)
        {
            // Inviting someone to our party - target must not be in a party already
            if (targetParty is not null)
            {
                if (targetParty.ContainsMember(requester.SessionId))
                {
                    await requester.Send(new PartyReplyServerPacket
                    {
                        ReplyCode = PartyReplyCode.AlreadyInYourParty,
                        ReplyCodeData = new PartyReplyServerPacket.ReplyCodeDataAlreadyInYourParty
                        {
                            PlayerName = target.Character.Name
                        }
                    });
                }
                else
                {
                    await requester.Send(new PartyReplyServerPacket
                    {
                        ReplyCode = PartyReplyCode.AlreadyInAnotherParty,
                        ReplyCodeData = new PartyReplyServerPacket.ReplyCodeDataAlreadyInAnotherParty
                        {
                            PlayerName = target.Character.Name
                        }
                    });
                }
                return;
            }

            // Check if inviter's party is full
            if (requesterParty is not null && requesterParty.MemberCount >= MaxPartySize)
            {
                await requester.Send(new PartyReplyServerPacket
                {
                    ReplyCode = PartyReplyCode.PartyIsFull,
                    ReplyCodeData = null
                });
                return;
            }
        }
        else if (type == PartyRequestType.Join)
        {
            // Requesting to join target's party
            if (targetParty is not null && targetParty.ContainsMember(requester.SessionId))
            {
                await requester.Send(new PartyReplyServerPacket
                {
                    ReplyCode = PartyReplyCode.AlreadyInYourParty,
                    ReplyCodeData = new PartyReplyServerPacket.ReplyCodeDataAlreadyInYourParty
                    {
                        PlayerName = target.Character.Name
                    }
                });
                return;
            }

            // Check if target's party is full
            if (targetParty is not null && targetParty.MemberCount >= MaxPartySize)
            {
                await requester.Send(new PartyReplyServerPacket
                {
                    ReplyCode = PartyReplyCode.PartyIsFull,
                    ReplyCodeData = null
                });
                return;
            }
        }

        // Store the pending request on the target
        _pendingRequests[targetSessionId] = (requester.SessionId, type);

        logger.LogInformation("Player {Requester} sent party {Type} request to {Target}",
            requester.Character.Name, type, target.Character.Name);

        await target.Send(new PartyRequestServerPacket
        {
            RequestType = type,
            InviterPlayerId = requester.SessionId,
            PlayerName = requester.Character.Name
        });
    }

    public async Task AcceptPartyRequest(PlayerState player, int requesterSessionId, PartyRequestType type)
    {
        if (player.Character is null)
        {
            return;
        }

        // Validate pending request
        if (!_pendingRequests.TryRemove(player.SessionId, out var pending))
        {
            return;
        }

        if (pending.RequesterSessionId != requesterSessionId || pending.Type != type)
        {
            return;
        }

        var requester = worldState.GetPlayer(requesterSessionId);
        if (requester?.Character is null)
        {
            return;
        }

        logger.LogInformation("Player {Player} accepted party {Type} request from {Requester}",
            player.Character.Name, type, requester.Character.Name);

        if (type == PartyRequestType.Invite)
        {
            // Requester invited us - check if requester is in a party
            var requesterParty = GetPlayerParty(requesterSessionId);
            if (requesterParty is not null)
            {
                await JoinParty(player, requesterParty);
            }
            else
            {
                await CreateParty(requesterSessionId, player.SessionId);
            }
        }
        else // Join
        {
            // Requester wants to join our party
            var playerParty = GetPlayerParty(player.SessionId);
            if (playerParty is not null)
            {
                await JoinParty(requester, playerParty);
            }
            else
            {
                await CreateParty(player.SessionId, requesterSessionId);
            }
        }
    }

    public async Task RemoveFromParty(PlayerState player, int targetSessionId)
    {
        var party = GetPlayerParty(player.SessionId);
        if (party is null)
        {
            return;
        }

        var isLeader = party.LeaderSessionId == player.SessionId;
        var isSelf = player.SessionId == targetSessionId;

        // If leader leaving, or only 2 members, disband
        if ((isLeader && isSelf) || party.MemberCount <= 2)
        {
            await DisbandParty(party);
            return;
        }

        // Non-leader can only remove themselves; leader can kick others
        if (!isSelf && !isLeader)
        {
            return;
        }

        await LeaveParty(targetSessionId, party);
    }

    public async Task RefreshPartyList(PlayerState player)
    {
        var party = GetPlayerParty(player.SessionId);
        if (party is null)
        {
            return;
        }

        await player.Send(new PartyListServerPacket
        {
            Members = BuildMemberList(party)
        });
    }

    public async Task BroadcastHpUpdate(PlayerState player)
    {
        if (player.Character is null)
        {
            return;
        }

        var party = GetPlayerParty(player.SessionId);
        if (party is null)
        {
            return;
        }

        var hpPercentage = player.Character.MaxHp > 0
            ? (int)((double)player.Character.Hp / player.Character.MaxHp * 100)
            : 0;

        var packet = new PartyAgreeServerPacket
        {
            PlayerId = player.SessionId,
            HpPercentage = hpPercentage
        };

        var tasks = party.Members
            .Select(worldState.GetPlayer)
            .Where(p => p is not null)
            .Select(p => p!.Send(packet));

        await Task.WhenAll(tasks);
    }

    public async Task DistributeExp(int killerSessionId, int totalExp, int mapId)
    {
        var party = GetPlayerParty(killerSessionId);
        if (party is null)
        {
            return;
        }

        // Only members on the same map get exp
        var membersOnMap = party.Members
            .Select(worldState.GetPlayer)
            .Where(p => p?.Character is not null && p.Character.Map == mapId)
            .ToList();

        if (membersOnMap.Count == 0)
        {
            return;
        }

        var expPerMember = Math.Max(1, totalExp / membersOnMap.Count);

        var gains = new List<PartyExpShare>();
        foreach (var member in membersOnMap)
        {
            if (member?.Character is null) continue;

            member.Character.Exp += expPerMember;
            gains.Add(new PartyExpShare
            {
                PlayerId = member.SessionId,
                Experience = expPerMember
            });
        }

        var packet = new PartyTargetGroupServerPacket
        {
            Gains = gains
        };

        var tasks = party.Members
            .Select(worldState.GetPlayer)
            .Where(p => p is not null)
            .Select(p => p!.Send(packet));

        await Task.WhenAll(tasks);
    }

    public async Task SendPartyMessage(PlayerState sender, string message)
    {
        if (sender.Character is null)
        {
            return;
        }

        var party = GetPlayerParty(sender.SessionId);
        if (party is null)
        {
            return;
        }

        var packet = new TalkOpenServerPacket
        {
            PlayerId = sender.SessionId,
            Message = message
        };

        var tasks = party.Members
            .Where(id => id != sender.SessionId)
            .Select(worldState.GetPlayer)
            .Where(p => p is not null)
            .Select(p => p!.Send(packet));

        await Task.WhenAll(tasks);
    }

    public async Task HandlePlayerDisconnect(PlayerState player)
    {
        var party = GetPlayerParty(player.SessionId);
        if (party is null)
        {
            return;
        }

        // Clean up pending requests
        _pendingRequests.TryRemove(player.SessionId, out _);

        if (party.MemberCount <= 2)
        {
            await DisbandParty(party);
        }
        else
        {
            await LeaveParty(player.SessionId, party);
        }
    }

    private async Task CreateParty(int leaderSessionId, int memberSessionId)
    {
        var leader = worldState.GetPlayer(leaderSessionId);
        var member = worldState.GetPlayer(memberSessionId);

        if (leader?.Character is null || member?.Character is null)
        {
            return;
        }

        var party = new Party(leaderSessionId, memberSessionId);
        _parties.Add(party);

        logger.LogInformation("Party created: leader={Leader}, member={Member}",
            leader.Character.Name, member.Character.Name);

        var packet = new PartyCreateServerPacket
        {
            Members = BuildMemberList(party)
        };

        await Task.WhenAll(leader.Send(packet), member.Send(packet));
    }

    private async Task JoinParty(PlayerState joiner, Party party)
    {
        if (joiner.Character is null)
        {
            return;
        }

        party.AddMember(joiner.SessionId);

        logger.LogInformation("Player {Player} joined party (leader={Leader})",
            joiner.Character.Name, party.LeaderSessionId);

        var hpPercentage = joiner.Character.MaxHp > 0
            ? (int)((double)joiner.Character.Hp / joiner.Character.MaxHp * 100)
            : 0;

        // Notify existing members about the new member
        var addPacket = new PartyAddServerPacket
        {
            Member = new PartyMember
            {
                PlayerId = joiner.SessionId,
                Leader = false,
                Level = joiner.Character.Level,
                HpPercentage = hpPercentage,
                Name = joiner.Character.Name
            }
        };

        var notifyTasks = party.Members
            .Where(id => id != joiner.SessionId)
            .Select(worldState.GetPlayer)
            .Where(p => p is not null)
            .Select(p => p!.Send(addPacket));

        await Task.WhenAll(notifyTasks);

        // Send full member list to the joiner
        await joiner.Send(new PartyCreateServerPacket
        {
            Members = BuildMemberList(party)
        });
    }

    private async Task LeaveParty(int sessionId, Party party)
    {
        party.RemoveMember(sessionId);

        var leaver = worldState.GetPlayer(sessionId);
        if (leaver is not null)
        {
            await leaver.Send(new PartyCloseServerPacket());
        }

        // Notify remaining members
        var removePacket = new PartyRemoveServerPacket
        {
            PlayerId = sessionId
        };

        var tasks = party.Members
            .Select(worldState.GetPlayer)
            .Where(p => p is not null)
            .Select(p => p!.Send(removePacket));

        await Task.WhenAll(tasks);
    }

    private async Task DisbandParty(Party party)
    {
        // Remove party from list - rebuild bag without it
        var removePacket = new PartyRemoveServerPacket
        {
            PlayerId = party.LeaderSessionId
        };

        var members = party.Members;

        // Remove all members so party is empty (won't match GetPlayerParty anymore)
        foreach (var memberId in members)
        {
            party.RemoveMember(memberId);
        }

        var tasks = members
            .Select(worldState.GetPlayer)
            .Where(p => p is not null)
            .Select(p => p!.Send(removePacket));

        await Task.WhenAll(tasks);

        logger.LogInformation("Party disbanded");
    }

    private List<PartyMember> BuildMemberList(Party party)
    {
        return party.Members
            .Select(id => (Id: id, Player: worldState.GetPlayer(id)))
            .Where(x => x.Player?.Character is not null)
            .Select(x =>
            {
                var c = x.Player!.Character!;
                var hpPct = c.MaxHp > 0 ? (int)((double)c.Hp / c.MaxHp * 100) : 0;
                return new PartyMember
                {
                    PlayerId = x.Id,
                    Leader = x.Id == party.LeaderSessionId,
                    Level = c.Level,
                    HpPercentage = hpPct,
                    Name = c.Name
                };
            })
            .ToList();
    }
}
