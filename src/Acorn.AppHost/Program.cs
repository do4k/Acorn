using System.Runtime.InteropServices;

// The Aspire dashboard ships its own copy of the Blazor browser JS under
// wwwroot/framework/blazor.web.{major}.js. That bundled file can be older than the
// ASP.NET Core runtime actually in use (e.g. Aspire 13.5.3 bundles a .NET 11 preview
// blazor.web.11.js while the installed runtime is .NET 11 RC1). When the Virtualize
// interop signatures drift the dashboard's Resources grid silently renders empty rows
// because the `OnSpacerBeforeVisible` JSInvokable call fails. Copy the runtime's
// matching blazor.web.js over the bundled copy before the dashboard starts.
PatchAspireDashboardBlazorJs();

var builder = DistributedApplication.CreateBuilder(args);

// Orchestrate the game server and its REST API.
//
// The game server is a plain TCP + WebSocket host (not an ASP.NET Core app), so it
// does not auto-register any endpoints. Declare its real ports here so the resource
// shows up with an endpoint/service in the Aspire dashboard and clients know where
// to connect. The ports are read from config at runtime (Server:Hosting:{Port,WebSocketPort}).
var server = builder.AddProject<Projects.Acorn>("acorn-server")
    // Non-proxied: the server binds these exact ports itself (read from config), so
    // DCP only records them for display/service-discovery rather than running a proxy
    // that would collide with the server's own listener.
    .WithEndpoint(port: 8078, name: "tcp", isProxied: false)
    .WithEndpoint(port: 8079, name: "websocket", isProxied: false);

// The REST API is an ASP.NET Core host - wire an externally reachable HTTP endpoint
// so it is easy to open in the browser and not stuck on loopback.
var api = builder.AddProject<Projects.Acorn_Api>("acorn-api")
    .WithExternalHttpEndpoints();

// Optional backing services (requires Docker). Uncomment to exercise the PostgreSQL
// and Redis providers during development instead of the SQLite + in-memory defaults.
// var db = builder.AddPostgres("postgres").WithDatabase("acorn");
// var cache = builder.AddRedis("cache");
// server.WithReference(db).WithReference(cache).WithEnvironment("Database__Engine", "PostgreSQL");
// api.WithReference(db).WithReference(cache);

builder.Build().Run();

static void PatchAspireDashboardBlazorJs()
{
    try
    {
        var nugetRoot = Environment.GetEnvironmentVariable("NUGET_PACKAGES");
        if (string.IsNullOrEmpty(nugetRoot))
        {
            nugetRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".nuget", "packages");
        }

        if (!Directory.Exists(nugetRoot))
        {
            return;
        }

        // The framework's Blazor JS is restored from the Microsoft.AspNetCore.App.Internal.Assets
        // package, whose version matches the ASP.NET Core runtime (e.g. 11.0.0-rc.1.26425.128).
        var assetsRoot = Path.Combine(nugetRoot, "microsoft.aspnetcore.app.internal.assets");
        if (!Directory.Exists(assetsRoot))
        {
            return;
        }

        var runtimeVersion = RuntimeInformation.FrameworkDescription
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault(token => token.Length > 0 && char.IsDigit(token[0]));

        var frameworkJs = runtimeVersion is null
            ? null
            : Path.Combine(assetsRoot, runtimeVersion, "staticwebassets", "_framework", "blazor.web.js");

        if (frameworkJs is null || !File.Exists(frameworkJs))
        {
            frameworkJs = Directory
                .EnumerateFiles(assetsRoot, "blazor.web.js", SearchOption.AllDirectories)
                .FirstOrDefault();
        }

        if (frameworkJs is null || !File.Exists(frameworkJs))
        {
            return;
        }

        var majorVersion = Environment.Version.Major;
        var frameworkBytes = File.ReadAllBytes(frameworkJs);

        foreach (var sdkDirectory in Directory.EnumerateDirectories(nugetRoot, "aspire.dashboard.sdk.*"))
        {
            foreach (var versionDirectory in Directory.EnumerateDirectories(sdkDirectory))
            {
                var dashboardJs = Path.Combine(
                    versionDirectory, "tools", "wwwroot", "framework", $"blazor.web.{majorVersion}.js");

                if (!File.Exists(dashboardJs) ||
                    File.ReadAllBytes(dashboardJs).SequenceEqual(frameworkBytes))
                {
                    continue;
                }

                File.Copy(frameworkJs, dashboardJs, overwrite: true);
                Console.WriteLine($"Patched Aspire dashboard Blazor JS from runtime: {dashboardJs}");
            }
        }
    }
    catch (Exception ex)
    {
        // Best effort - never block the AppHost on this workaround.
        Console.WriteLine($"Warning: could not patch Aspire dashboard Blazor JS: {ex.Message}");
    }
}
