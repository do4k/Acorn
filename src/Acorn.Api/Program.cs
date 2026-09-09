using Acorn.Api.Features;
using Acorn.Database;
using Acorn.Database.Models;
using Acorn.Database.Repository;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Database + caching infrastructure: options binding, DbContext and in-memory cache
builder.Services.AddAcornDataInfrastructure(builder.Configuration);

// Register repositories for database access
builder.Services.AddScoped<IDbRepository<Character>, CharacterRepository>();

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
