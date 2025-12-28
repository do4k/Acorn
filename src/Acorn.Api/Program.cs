using Acorn.Api.Features;
using Acorn.Shared.Extensions;
using Acorn.Shared.Options;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var configuration = builder.Configuration;

// Configure options
builder.Services.Configure<CacheOptions>(configuration.GetSection("Cache"));

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
