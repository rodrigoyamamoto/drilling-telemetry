using DrillingTelemetry.Processor.Configuration;
using DrillingTelemetry.Processor.Messaging;


HostApplicationBuilder builder =
    Host.CreateApplicationBuilder(args);

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

IHost host = builder.Build();

await host.RunAsync();
