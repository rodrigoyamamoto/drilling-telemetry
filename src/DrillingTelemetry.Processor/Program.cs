using DrillingTelemetry.Processor.Configuration;
using DrillingTelemetry.Processor.Realtime;

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

app.UseCors();

app.MapHub<TelemetryHub>(
    TelemetryHub.RoutePattern);

await app.RunAsync();
