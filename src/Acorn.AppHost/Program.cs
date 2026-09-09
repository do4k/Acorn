var builder = DistributedApplication.CreateBuilder(args);

// Orchestrate the game server and its REST API.
var server = builder.AddProject<Projects.Acorn>("acorn-server");
var api = builder.AddProject<Projects.Acorn_Api>("acorn-api");

// Optional backing services (requires Docker). Uncomment to exercise the PostgreSQL
// and Redis providers during development instead of the SQLite + in-memory defaults.
// var db = builder.AddPostgres("postgres").WithDatabase("acorn");
// var cache = builder.AddRedis("cache");
// server.WithReference(db).WithReference(cache).WithEnvironment("Database__Engine", "PostgreSQL");
// api.WithReference(db).WithReference(cache);

builder.Build().Run();
