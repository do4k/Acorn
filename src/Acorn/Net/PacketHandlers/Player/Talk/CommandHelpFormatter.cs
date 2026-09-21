using Acorn.Net.Services;

namespace Acorn.Net.PacketHandlers.Player.Talk;

/// <summary>
///     Formats command metadata for the <c>$help</c> / <c>#help</c> commands.
/// </summary>
internal static class CommandHelpFormatter
{
    public static string Describe(ICommandHandler handler, char prefix)
    {
        var primary = handler.Commands.Count > 0 ? handler.Commands[0] : string.Empty;
        var name = $"{prefix}{primary}";

        if (handler.Commands.Count > 1)
        {
            name += $" ({string.Join(", ", handler.Commands.Skip(1).Select(alias => $"{prefix}{alias}"))})";
        }

        if (handler.Usage.Length > 0)
        {
            name += $" {handler.Usage}";
        }

        var description = CommandDescriptions.Get(prefix, primary);
        return description is null ? name : $"{name} - {description}";
    }

    public static IEnumerable<string> DescribeAll(IEnumerable<ICommandHandler> handlers, char prefix)
        => handlers
            .OrderBy(h => h.Commands.Count > 0 ? h.Commands[0] : string.Empty, StringComparer.OrdinalIgnoreCase)
            .Select(h => Describe(h, prefix));

    public static async Task SendPackedAsync(INotificationService notifications, PlayerState player,
        string header, IEnumerable<string> lines, int maxWidth = 100)
    {
        await notifications.SystemMessage(player, header);

        var buffer = string.Empty;
        foreach (var line in lines)
        {
            if (buffer.Length == 0)
            {
                buffer = line;
                continue;
            }

            if (buffer.Length + 3 + line.Length > maxWidth)
            {
                await notifications.SystemMessage(player, buffer);
                buffer = line;
            }
            else
            {
                buffer += " | " + line;
            }
        }

        if (buffer.Length > 0)
        {
            await notifications.SystemMessage(player, buffer);
        }
    }
}
