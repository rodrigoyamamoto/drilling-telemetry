using DrillingTelemetry.Control.Api.Configuration;
using DrillingTelemetry.Control.Api.Endpoints;
using Scalar.AspNetCore;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddControlApi(
    builder.Configuration);

WebApplication app = builder.Build();

app.UseExceptionHandler();
app.UseCors();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    app.MapScalarApiReference(options =>
        options.WithTitle(
            "Drilling Telemetry Control API"));
}

app.MapSimulationSettingsEndpoints();

await app.RunAsync();
