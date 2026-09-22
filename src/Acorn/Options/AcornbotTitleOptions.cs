namespace Acorn.Options;

/// <summary>
///     Configuration for the Acornbot <c>title</c> command. Costs default to free;
///     set <see cref="CostItemId" /> to the required item (1 for gold) with a
///     positive <see cref="CostAmount" /> to charge players per title change.
/// </summary>
public class AcornbotTitleOptions
{
    /// <summary>
    ///     Maximum number of characters accepted in a title.
    /// </summary>
    public int MaxLength { get; set; } = 32;

    /// <summary>
    ///     Item id the command consumes as its cost. Use 1 (gold) to charge a money
    ///     price, or another id for a "title certificate" style item.
    /// </summary>
    public int CostItemId { get; set; } = 1;

    /// <summary>
    ///     Quantity of <see cref="CostItemId" /> consumed per title change.
    ///     Zero (the default) makes the command free.
    /// </summary>
    public int CostAmount { get; set; } = 0;
}
