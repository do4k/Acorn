using Acorn.Plugins;

namespace Acorn.Tests.Plugins;

/// <summary>
///     Hook-recording plugin fake: records every invocation into a shared list,
///     optionally throwing to exercise error isolation and auto-disable.
/// </summary>
internal sealed class RecordingHookPlugin(
    List<string> recorder,
    string name,
    int priority = 0)
    : IAcornPlugin, IWorldTickHook, IMapTickHook
{
    public int Priority => priority;

    /// <summary>When true every hook invocation records and then throws.</summary>
    public bool Throws { get; set; }

    public Task OnLoadedAsync(IPluginContext context, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task OnUnloadingAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task OnWorldTickAsync(WorldTickContext context)
    {
        Record("world");
        return Task.CompletedTask;
    }

    public Task OnMapTickAsync(MapTickContext context)
    {
        Record("map");
        return Task.CompletedTask;
    }

    private void Record(string hook)
    {
        recorder.Add($"{name}:{hook}");
        if (Throws)
        {
            throw new InvalidOperationException($"{name} exploded in {hook}");
        }
    }
}
