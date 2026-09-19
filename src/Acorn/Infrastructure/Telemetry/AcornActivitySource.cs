using System.Diagnostics;

namespace Acorn.Infrastructure.Telemetry;

/// <summary>
///     Central <see cref="ActivitySource" /> for game-server spans. Hot paths such as
///     packet dispatch and the world tick create activities from here; the tracer is
///     registered with <see cref="Name" /> in Program.cs.
/// </summary>
public static class AcornActivitySource
{
    public const string Name = "Acorn";

    public static readonly ActivitySource Instance = new(Name, "1.0.0");
}
