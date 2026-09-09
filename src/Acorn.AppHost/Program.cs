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
