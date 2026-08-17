using DrillingTelemetry.Processor.Configuration;
using DrillingTelemetry.Processor.Realtime;

WebApplicationBuilder builder =
    WebApplication.CreateBuilder(args);

builder.Services.AddTelemetryProcessor(
    builder.Configuration);

WebApplication app = builder.Build();

app.UseCors();

app.MapHub<TelemetryHub>(
    TelemetryHub.RoutePattern);

await app.RunAsync();
