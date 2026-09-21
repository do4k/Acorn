namespace Acorn.World.Services.Guild;

/// <summary>
///     Outcome of an admin-initiated guild creation via
///     <see cref="IGuildService.AdminCreateGuild" />.
/// </summary>
public enum AdminCreateGuildResult
{
    /// <summary>The guild was created and the player is its leader.</summary>
    Created,

    /// <summary>The tag or name failed validation.</summary>
    InvalidTagOrName,

    /// <summary>The player is already a member of a guild.</summary>
    AlreadyInGuild,

    /// <summary>A guild with the same tag or name already exists.</summary>
    GuildExists
}
