using Acorn.Api.Features;
using Acorn.Database;
using Acorn.Database.Models;
using Acorn.Database.Repository;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Database + caching infrastructure: options binding, DbContext and in-memory cache
builder.Services.AddAcornDataInfrastructure(builder.Configuration);

// Register repositories for database access
builder.Services.AddScoped<IDbRepository<Character>, CharacterRepository>();

// Export metrics and traces via OTLP (consumed by the Aspire dashboard)
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService("acorn-api"))
    .WithMetrics(metrics =>
    {
        metrics.AddAspNetCoreInstrumentation();
        metrics.AddRuntimeInstrumentation();
    })
    .WithTracing(tracing =>
    {
        tracing.AddAspNetCoreInstrumentation();
    })
    .UseOtlpExporter();

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
