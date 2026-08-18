using DrillingTelemetry.Control.Api.Publishing;
using DrillingTelemetry.Control.Api.RuntimeSettings;

namespace DrillingTelemetry.Control.Api.Configuration;

/// <summary>
/// Provides dependency injection configuration for the Control API.
/// </summary>
internal static class ControlApiServiceCollectionExtensions
{
    /// <summary>
    /// Adds the Control API services to the application.
    /// </summary>
    /// <param name="services">
    /// Collection receiving the Control API services.
    /// </param>
    /// <returns>
    /// The same service collection so registrations can be chained.
    /// </returns>
    public static IServiceCollection AddControlApi(
        this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddProblemDetails();
        services.AddOpenApi();
        services.AddValidation();

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
                        options.SimulationSettingsQueueName),
                "RabbitMQ simulation settings queue name is missing.")
            .ValidateOnStart();

        services.AddSingleton(TimeProvider.System);

        services.AddSingleton<
            ISimulationSettingsRevisionProvider,
            InMemorySimulationSettingsRevisionProvider>();

        services.AddSingleton<
            RabbitMqSimulationSettingsCommandPublisher>();

        services.AddSingleton<ISimulationSettingsCommandPublisher>(
            serviceProvider =>
                serviceProvider.GetRequiredService<
                    RabbitMqSimulationSettingsCommandPublisher>());

        services.AddSingleton<IHostedService>(
            serviceProvider =>
                serviceProvider.GetRequiredService<
                    RabbitMqSimulationSettingsCommandPublisher>());

        return services;
    }
}
