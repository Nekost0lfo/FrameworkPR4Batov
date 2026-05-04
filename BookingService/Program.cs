using BookingService.Api;
using BookingService.Health;
using BookingService.Middleware;
using BookingService.Options;
using BookingService.Services;
using BookingService.Telemetry;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using OpenTelemetry.Metrics;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<HealthOptions>(builder.Configuration.GetSection(HealthOptions.SectionName));
builder.Services.AddSingleton<ServiceHealthState>();
builder.Services.AddSingleton<InMemoryBookingStore>();
builder.Services.AddSingleton<BookingTelemetry>();
builder.Services.AddSingleton<BookingApplicationService>();

builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics =>
    {
        metrics.AddMeter(BookingTelemetry.MeterName);
        metrics.AddAspNetCoreInstrumentation();
        metrics.AddPrometheusExporter();
    });

builder.Services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy(), tags: ["live"])
    .AddCheck<ReadinessHealthCheck>("readiness", tags: ["ready"]);

builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

builder.Services.AddOpenApi();

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGet("/health/live", () => Results.Ok(new { status = "Alive", timestamp = DateTimeOffset.UtcNow }))
    .WithName("Liveness");

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = static check => check.Tags.Contains("ready"),
});

app.MapHealthChecks("/health/live-detailed", new HealthCheckOptions
{
    Predicate = static check => check.Tags.Contains("live"),
});

app.MapPrometheusScrapingEndpoint();

app.MapPost("/api/processes/{processKey}/events", (
        string processKey,
        PostEventRequest body,
        HttpContext http,
        BookingApplicationService booking) =>
    {
        var correlationId = http.GetRequiredCorrelationId();
        var result = booking.HandleEvent(
            processKey,
            body.IdempotencyKey,
            body.Event,
            correlationId,
            body.SimulateFailure);

        return Results.Ok(new PostEventResponse(
            result.CorrelationId,
            processKey,
            body.IdempotencyKey,
            result.State.ToString(),
            result.Applied,
            result.Duplicate,
            result.Compensated,
            result.Message));
    })
    .WithName("PostBookingEvent");

app.MapGet("/api/processes/{processKey}", (string processKey, InMemoryBookingStore store, BookingApplicationService booking) =>
    {
        if (!store.TryGet(processKey, out var instance) || instance is null)
        {
            return Results.NotFound(new { processKey, message = "Unknown process key." });
        }

        var snap = booking.GetSnapshot(processKey);
        return Results.Ok(new ProcessStatusResponse(
            snap.ProcessKey,
            snap.State.ToString(),
            snap.AppliedIdempotencyKeys,
            snap.LastError,
            snap.CompensationCount));
    })
    .WithName("GetProcessStatus");

app.Run();
