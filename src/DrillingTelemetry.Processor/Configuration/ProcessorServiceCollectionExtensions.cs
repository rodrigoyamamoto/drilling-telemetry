using DrillingTelemetry.Processor.Messaging;
using DrillingTelemetry.Processor.Diagnostics;
using DrillingTelemetry.Processor.Processing;
using DrillingTelemetry.Processor.Realtime;
using DrillingTelemetry.Processor.Sequencing;

namespace DrillingTelemetry.Processor.Configuration;

/// <summary>
/// Provides dependency injection configuration for the telemetry processor.
/// </summary>
internal static class ProcessorServiceCollectionExtensions
{
    private const string AllowedOriginsSectionName =
        "Cors:AllowedOrigins";

    /// <summary>
    /// Adds the telemetry processor services to the application.
    /// </summary>
    /// <param name="services">
    /// Collection receiving the telemetry processor services.
    /// </param>
    /// <param name="configuration">
    /// Provides the application configuration.
    /// </param>
    /// <returns>
    /// The same service collection so registrations can be chained.
    /// </returns>
    public static IServiceCollection AddTelemetryProcessor(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        string[] allowedOrigins = configuration
            .GetSection(AllowedOriginsSectionName)
            .Get<string[]>()
            ?? throw new InvalidOperationException(
                "CORS allowed origins are missing.");

        services
            .AddOptions<RabbitMqOptions>()
            .BindConfiguration(RabbitMqOptions.SectionName)
            .Validate(
                options =>
                    !string.IsNullOrWhiteSpace(options.HostName),
                "RabbitMQ host name is missing.")
            .Validate(
                options =>
                    !string.IsNullOrWhiteSpace(
                        options.TelemetryReadingsQueueName),
                "RabbitMQ telemetry readings queue name is missing.")
            .Validate(
                options =>
                    !string.IsNullOrWhiteSpace(
                        options.TelemetryDeadLetterExchangeName),
                "RabbitMQ telemetry dead-letter exchange name is missing.")
            .Validate(
                options =>
                    !string.IsNullOrWhiteSpace(
                        options.TelemetryDeadLetterQueueName),
                "RabbitMQ telemetry dead-letter queue name is missing.")
            .Validate(
                options =>
                    options.TelemetryPrefetchCount > 0,
                "RabbitMQ telemetry prefetch count must be greater than zero.")
            .Validate(
                options =>
                    options.TelemetryConsumerCount > 0,
                "RabbitMQ telemetry consumer count must be greater than zero.")
            .ValidateOnStart();

        services.AddSignalR();

        services.AddSingleton(TimeProvider.System);

        services.AddSingleton<TelemetryProcessingMetrics>();

        services.AddSingleton<TelemetrySequenceTracker>();

        services.AddSingleton<
            ITelemetryReadingProcessor,
            TelemetryReadingProcessor>();

        services.AddCors(options =>
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

        services.AddSingleton<
            ITelemetryReadingBroadcaster,
            SignalRTelemetryReadingBroadcaster>();

        services.AddHostedService<
            RabbitMqTelemetryReadingConsumer>();

        return services;
    }
}
