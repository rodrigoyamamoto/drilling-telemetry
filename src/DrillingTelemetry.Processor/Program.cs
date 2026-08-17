using DrillingTelemetry.Processor.Configuration;
using DrillingTelemetry.Processor.Messaging;
using DrillingTelemetry.Processor.Realtime;

WebApplicationBuilder builder =
    WebApplication.CreateBuilder(args);

string[] allowedOrigins =
    builder.Configuration
        .GetSection("Cors:AllowedOrigins")
        .Get<string[]>() ??
    throw new InvalidOperationException("CORS allowed origins are missing.");

builder.Services
    .AddOptions<RabbitMqOptions>()
    .BindConfiguration(RabbitMqOptions.SectionName)
    .Validate(
        options => !string.IsNullOrWhiteSpace(options.HostName),
        "RabbitMQ host name is missing.")
    .Validate(
        options => !string.IsNullOrWhiteSpace(
            options.TelemetryReadingsQueueName),
        "RabbitMQ telemetry readings queue name is missing.")
    .ValidateOnStart();

builder.Services
    .AddHostedService<RabbitMqTelemetryReadingConsumer>();

builder.Services.AddSignalR();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy
            .WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .WithMethods(
                HttpMethods.Get,
                HttpMethods.Post)
            .AllowCredentials();
    });
});

builder.Services.AddSingleton<
    ITelemetryReadingBroadcaster,
    SignalRTelemetryReadingBroadcaster>();

WebApplication app = builder.Build();

app.UseCors();

app.MapHub<TelemetryHub>(
    TelemetryHub.RoutePattern);

await app.RunAsync();
