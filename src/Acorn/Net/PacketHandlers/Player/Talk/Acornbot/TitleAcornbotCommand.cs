using System.Globalization;
using Acorn.Database.Repository;
using Acorn.Extensions;
using Acorn.Game.Mappers;
using Acorn.Game.Services;
using Acorn.Options;
using Acorn.Shared.Caching;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;
using Moffat.EndlessOnline.SDK.Protocol.Pub;

namespace Acorn.Net.PacketHandlers.Player.Talk.Acornbot;

/// <summary>
///     Acornbot's <c>title</c> command: <c>!acornbot title [title to be applied]</c>.
///     Applies a title to the whispering player, optionally consuming an item -
///     a "title certificate" style stackable, or gold when the configured cost
///     item id is 1. With no cost configured (the default) titles are free;
///     <c>title clear</c> removes the current title and never charges.
/// </summary>
public class TitleAcornbotCommand(
    IAcornbotReplyChannel replies,
    IOptions<AcornbotOptions> options,
    IBannedTextPolicy bannedText,
    IInventoryService inventoryService,
    IWeightCalculator weightCalculator,
    IDataFileRepository dataFiles,
    ICharacterCacheService characterCache,
    IPaperdollService paperdollService,
    IDbRepository<Database.Models.Character> characterRepository,
    ICharacterMapper characterMapper,
    ILogger<TitleAcornbotCommand> logger) : IAcornbotCommand
{
    /// <summary>
    ///     Inventory id of gold.
    /// </summary>
    private const int GoldItemId = 1;

    /// <summary>
    ///     Keyword that removes the player's current title instead of setting one.
    /// </summary>
    private const string ClearKeyword = "clear";

    public IReadOnlyList<string> Commands => ["title"];

    public string Usage => $"<title text> | {ClearKeyword}";

    public string Description => "Set the title shown under your name, or 'clear' to remove it.";

    public async Task HandleAsync(PlayerState playerState, string command, params string[] args)
    {
        var character = playerState.Character;
        if (character is null)
        {
            return;
        }

        var titleOptions = options.Value.Title;
        var title = Clean(args);

        if (title.Length == 0)
        {
            await replies.WhisperAsync(playerState, $"Usage: {command} <title text> | {ClearKeyword}");
            return;
        }

        var clearing = title.Equals(ClearKeyword, StringComparison.OrdinalIgnoreCase);

        if (!clearing && title.Length > titleOptions.MaxLength)
        {
            await replies.WhisperAsync(playerState,
                $"Titles can be at most {titleOptions.MaxLength} characters - yours is {title.Length}.");
            return;
        }

        if (!clearing && bannedText.FirstViolation(title) is { Length: > 0 } banned)
        {
            await replies.WhisperAsync(playerState,
                $"Titles cannot contain the symbol \"{banned}\".");
            return;
        }

        var charge = !clearing && titleOptions.CostAmount > 0;
        string? payment = null;

        if (charge)
        {
            var costItemId = titleOptions.CostItemId > 0 ? titleOptions.CostItemId : GoldItemId;

            if (!inventoryService.HasItem(character, costItemId, titleOptions.CostAmount))
            {
                await replies.WhisperAsync(playerState,
                    $"A title costs {Format(costItemId, titleOptions.CostAmount)} - you have {Format(costItemId, inventoryService.GetItemAmount(character, costItemId))}.");
                return;
            }

            if (!inventoryService.TryRemoveItem(character, costItemId, titleOptions.CostAmount))
            {
                await replies.WhisperAsync(playerState, "Your payment could not be collected; no title was set.");
                logger.LogWarning("Failed to collect {Quantity}x item {ItemId} from {Player} for a title",
                    titleOptions.CostAmount, costItemId, character.Name);
                return;
            }

            payment = Format(costItemId, titleOptions.CostAmount);
            await SendItemUpdateAsync(playerState, costItemId);
        }

        character.Title = clearing ? null : title;

        await characterRepository.UpdateAsync(characterMapper.ToDatabase(character));
        await playerState.CacheCharacterStateAsync(characterCache, paperdollService);

        if (playerState.CurrentMap is not null)
        {
            // Re-announce the player to nearby viewers so their nameplates pick up the
            // new title. Msg_Players/Agree is merged by player id client-side, so no
            // full re-warp (and warp animation) is needed.
            await playerState.CurrentMap.NotifyAppear(playerState);
        }

        logger.LogInformation("Acornbot set title '{Title}' for {Player} (payment: {Payment})",
            character.Title, character.Name, payment ?? "none");

        if (clearing)
        {
            await replies.WhisperAsync(playerState, "Your title has been cleared.");
        }
        else
        {
            await replies.WhisperAsync(playerState,
                payment is null
                    ? $"Your title is now \"{title}\"."
                    : $"Your title is now \"{title}\". Paid {payment}.");
        }
    }

    /// <summary>
    ///     Joins the arguments into the title text, dropping control characters.
    ///     The arguments are already whitespace-normalised by the caller's split.
    /// </summary>
    private static string Clean(string[] args) =>
        new string(string.Join(' ', args).Where(c => !char.IsControl(c)).ToArray()).Trim();

    private string CostName(int costItemId)
    {
        if (costItemId == GoldItemId)
        {
            return "gold";
        }

        return dataFiles.Eif.GetItem(costItemId)?.Name ?? $"item #{costItemId}";
    }

    private string Format(int costItemId, int amount) =>
        costItemId == GoldItemId
            ? $"{amount.ToString("N0", CultureInfo.InvariantCulture)} gold"
            : $"{amount}x {CostName(costItemId)}";

    /// <summary>
    ///     Syncs the consumed item (and current weight) to the client. Reuses the
    ///     item-use reply, which the client applies to the inventory slot without
    ///     any "used item" side effects for a General item type.
    /// </summary>
    private async Task SendItemUpdateAsync(PlayerState playerState, int costItemId)
    {
        var character = playerState.Character!;
        await playerState.Send(new ItemReplyServerPacket
        {
            ItemType = ItemType.General,
            UsedItem = new Moffat.EndlessOnline.SDK.Protocol.Net.Item
            {
                Id = costItemId,
                Amount = inventoryService.GetItemAmount(character, costItemId)
            },
            Weight = new Weight
            {
                Current = weightCalculator.GetCurrentWeight(character, dataFiles.Eif),
                Max = character.MaxWeight
            }
        });
    }
}
