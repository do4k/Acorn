using Acorn.Database.Models;
using Acorn.Game.Validation;
using Acorn.Infrastructure.Security;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;

namespace Acorn.Extensions;

public static class AccountCreateClientPacketExtensions
{
    public static Account AsNewAccount(this AccountCreateClientPacket packet, DateTime created)
    {
        // Usernames are normalized to lowercase so lookups and password hashes
        // are case-insensitive, matching eoserv.
        var username = PlayerValidation.NormalizeName(packet.Username);
        var password = Hash.HashPassword(username, packet.Password, out var salt);
        return new Account
        {
            Characters = new List<Character>(),
            Country = packet.Location,
            Created = created,
            Email = packet.Email,
            FullName = packet.FullName,
            LastUsed = created,
            Location = packet.Location,
            Password = password,
            Salt = Convert.ToBase64String(salt),
            Username = username
        };
    }
}