namespace Acorn.World.Services.Guild;

/// <summary>
///     Outcome of an admin-initiated guild creation via
///     <see cref="IGuildService.AdminCreateGuild" />.
/// </summary>
public enum AdminCreateGuildResult
{
    /// <summary>The guild was created and the player is its leader.</summary>
    Created,

    /// <summary>The tag, name or description failed validation.</summary>
    InvalidInput,

    /// <summary>The player is already a member of a guild.</summary>
    AlreadyInGuild,

    /// <summary>A guild with the same tag or name already exists.</summary>
    GuildExists
}
