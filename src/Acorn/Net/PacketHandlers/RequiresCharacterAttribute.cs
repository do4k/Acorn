namespace Acorn.Net.PacketHandlers;

/// <summary>
/// Marks a packet handler as requiring the player to have a character and map loaded.
/// The dispatch pipeline will reject packets from players without a character/map
/// before the handler is invoked, with a centralized warning log.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class RequiresCharacterAttribute : Attribute;
