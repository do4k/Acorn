using System.Reflection;
using Acorn.Api.Features;
using Acorn.Database;
using Acorn.Database.Models;
using Acorn.Database.Repository;
using OpenTelemetry;
using OpenTelemetry.Logs;
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

// Export metrics, traces and logs via OTLP. The endpoint comes from
// OTEL_EXPORTER_OTLP_ENDPOINT (consumed by the Aspire dashboard in development
// or by an OTel Collector / SigNoz in production).
var serviceVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0";
var sampleRatio = builder.Configuration.GetValue("Telemetry:SampleRatio", 1.0);

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService("acorn-api", serviceVersion: serviceVersion,
            serviceInstanceId: $"{Environment.MachineName}:{Environment.ProcessId}")
        .AddAttributes(new Dictionary<string, object>
        {
            ["deployment.environment"] = builder.Environment.EnvironmentName,
            ["host.name"] = Environment.MachineName
        }))
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddRuntimeInstrumentation()
        .AddProcessInstrumentation())
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddEntityFrameworkCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .SetSampler(new ParentBasedSampler(new TraceIdRatioBasedSampler(sampleRatio))))
    .WithLogging(
        _ => { },
        logging =>
        {
            // Correlate structured logs with the active trace/span.
            logging.IncludeScopes = true;
            logging.IncludeFormattedMessage = true;
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
