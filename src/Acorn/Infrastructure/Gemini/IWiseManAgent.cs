using Acorn.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Acorn.Infrastructure.Gemini;

/// <summary>
///     Service for generating AI responses using Gemini.
/// </summary>
public interface IWiseManAgent
{
    /// <summary>
    ///     Generate a response from the Wise Man NPC.
    /// </summary>
    Task<string?> GetWiseManResponseAsync(string playerName, string query);
}
