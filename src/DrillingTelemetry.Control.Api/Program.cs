using DrillingTelemetry.Control.Api.Configuration;
using DrillingTelemetry.Control.Api.Endpoints;
using DrillingTelemetry.Control.Api.Publishing;
using RabbitMQ.Client;
using Scalar.AspNetCore;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

RabbitMqOptions rabbitMqOptions =
    builder.Configuration
        .GetRequiredSection(RabbitMqOptions.SectionName)
        .Get<RabbitMqOptions>() ??
    throw new InvalidOperationException(
        "RabbitMQ configuration is missing.");

if (string.IsNullOrWhiteSpace(rabbitMqOptions.HostName))
{
    throw new InvalidOperationException(
        "RabbitMQ host name is missing.");
}

if (string.IsNullOrWhiteSpace(
        rabbitMqOptions.SettingsQueueName))
{
    throw new InvalidOperationException(
        "RabbitMQ settings queue name is missing.");
}

var connectionFactory = new ConnectionFactory
{
    HostName = rabbitMqOptions.HostName
};

await using IConnection connection =
    await connectionFactory.CreateConnectionAsync();

await using IChannel channel =
    await connection.CreateChannelAsync();

await channel.QueueDeclareAsync(
    queue: rabbitMqOptions.SettingsQueueName,
    durable: true,
    exclusive: false,
    autoDelete: false,
    arguments: null);

var commandPublisher =
    new RabbitMqSimulationSettingsCommandPublisher(
        channel,
        rabbitMqOptions.SettingsQueueName);

builder.Services
    .AddSingleton<ISimulationSettingsCommandPublisher>(
        commandPublisher);

WebApplication app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    app.MapScalarApiReference(options =>
        options.WithTitle(
            "Drilling Telemetry Control API"));
}

app.MapSimulationSettingsEndpoints();

await app.RunAsync();
