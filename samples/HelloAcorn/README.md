# Hello Acorn — sample plugin

Demonstrates the Phase 0 plugin surface: lifecycle (`IAcornPlugin`), world/map tick
hooks (`IWorldTickHook`, `IMapTickHook`) and a player chat command (`#hello`).

## Build & install

Publish the plugin into the server's plugin directory:

```bash
# from the repo root; output goes next to the server binary used by `dotnet run`
dotnet publish samples/HelloAcorn -c Release -o src/Acorn/bin/Debug/net11.0/plugins/hello-acorn
```

(Deployed servers: publish into `<app>/plugins/hello-acorn` instead — the plugin
directory is probed at the server base directory first, then the working directory.)

Enable it in `src/Acorn/appsettings.json` (or via environment):

```jsonc
"Plugins": {
  "Enabled": true,
  "Directory": "plugins",
  "Load": [ "hello-acorn" ]
}
```

```bash
Plugins__Load__0=hello-acorn dotnet run --project src/Acorn
```

## Try it in game

Start the server — the startup banner prints `Plugins: Hello Acorn 1.0.0` and the log
shows `Loaded plugin Hello Acorn 1.0.0 (id: hello-acorn, ...)`. Connect a character
and type:

```
#hello
```

The plugin replies with your position and the online player count. With debug logging
you will also see a `World tick ...` heartbeat every ~10 seconds.

## Building your own plugin

- Reference the `Acorn.Plugins` contracts with `<Private>false</Private>` and
  `<ExcludeAssets>runtime</ExcludeAssets>` (see `HelloAcorn.csproj`) — the host
  provides them at runtime and copies break type identity.
- Set `<EnableDynamicLoading>true</EnableDynamicLoading>`.
- Ship a `plugin.json` next to the entry assembly with your plugin `id` (must match
  the folder name), `version`, `entryAssembly` and the `contractVersion` the plugin
  was built against.
- Implement exactly one public `IAcornPlugin`; add hook interfaces
  (`IWorldTickHook`, `IMapTickHook`, …) and `IPluginCommand` to the same class.

See [docs/PLUGINS.md](../../docs/PLUGINS.md) for the architecture and roadmap.
