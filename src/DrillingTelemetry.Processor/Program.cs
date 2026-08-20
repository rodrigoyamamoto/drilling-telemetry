using DrillingTelemetry.Processor.Configuration;
using DrillingTelemetry.Processor.Realtime;
using DrillingTelemetry.Processor.Endpoints;
using Scalar.AspNetCore;

string? environmentName =
    Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ??
    Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");

if (string.Equals(
        environmentName,
        Environments.Development,
        StringComparison.OrdinalIgnoreCase))
{
    DotNetEnv.Env
        .NoClobber()
        .TraversePath()
        .Load();
}

WebApplicationBuilder builder =
    WebApplication.CreateBuilder(args);

builder.Services.AddTelemetryProcessor(
    builder.Configuration);

WebApplication app = builder.Build();

app.UseExceptionHandler();
app.UseCors();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    app.MapScalarApiReference(options =>
        options.WithTitle(
            "Drilling Telemetry Processor API"));
}

app.MapTelemetryReadingsEndpoints();
app.MapOperationalEventsEndpoints();

app.MapHub<TelemetryHub>(
    TelemetryHub.RoutePattern);

await app.RunAsync();
