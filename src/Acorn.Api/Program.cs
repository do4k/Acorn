using Acorn.Api.Features;
using Acorn.Database;
using Acorn.Database.Models;
using Acorn.Database.Repository;
using Acorn.Shared.Extensions;
using Acorn.Shared.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var configuration = builder.Configuration;

// Configure options
builder.Services.Configure<CacheOptions>(configuration.GetSection("Cache"));
builder.Services.Configure<DatabaseOptions>(configuration.GetSection(DatabaseOptions.SectionName));

// Configure Database (same as Acorn core)
builder.Services.AddDbContext<AcornDbContext>((sp, options) =>
{
    var dbOptions = sp.GetRequiredService<IOptions<DatabaseOptions>>().Value;
    var connectionString = dbOptions.ConnectionString;
    var dbEngine = dbOptions.Engine?.ToLower() ?? "sqlite";

    options.UseDatabaseEngine(dbEngine, connectionString);
});

// Register repositories for database access
builder.Services.AddScoped<IDbRepository<Character>, CharacterRepository>();

// Configure Caching (Redis or In-Memory) - same as Acorn core
builder.Services.AddCaching();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Map feature endpoints
app.MapHealthEndpoints();
app.MapCharacterEndpoints();
app.MapMapEndpoints();
app.MapOnlinePlayersEndpoints();
app.MapPubEndpoints();

app.Run();
