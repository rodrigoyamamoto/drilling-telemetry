using DrillingTelemetry.DeviceSimulator.Generation;
using DrillingTelemetry.DeviceSimulator.Diagnostics;
using DrillingTelemetry.DeviceSimulator.Runtime;
using DrillingTelemetry.DeviceSimulator.RuntimeSettings;
using DrillingTelemetry.DeviceSimulator.Simulation;
using Microsoft.Extensions.Options;

namespace DrillingTelemetry.DeviceSimulator.Configuration;

/// <summary>
/// Provides dependency injection configuration for the device simulator.
/// </summary>
internal static class DeviceSimulatorServiceCollectionExtensions
{
    private const long InitialSettingsRevision = 1;

    /// <summary>
    /// Adds the device simulator services to the application.
    /// </summary>
    /// <param name="services">
    /// Collection receiving the device simulator services.
    /// </param>
    /// <returns>
    /// The same service collection so registrations can be chained.
    /// </returns>
    public static IServiceCollection AddDeviceSimulator(
        this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

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
                        options.SimulationSettingsExchangeName),
                "RabbitMQ simulation settings exchange name is missing.")
            .ValidateOnStart();

        services
            .AddOptions<SimulationOptions>()
            .BindConfiguration(SimulationOptions.SectionName)
            .Validate(
                options =>
                    options.DeviceIds is { Length: > 0 } &&
                    options.DeviceIds.All(deviceId =>
                        !string.IsNullOrWhiteSpace(deviceId)),
                "At least one valid device must be configured.")
            .Validate(
                options =>
                    options.PublishingIntervalMilliseconds > 0,
                "Publishing interval must be greater than zero.")
            .Validate(
                options =>
                    Enum.IsDefined(options.GenerationMode),
                "Telemetry generation mode is invalid.")
            .Validate(
                options =>
                    options.MinimumPressurePsi <
                    options.MaximumPressurePsi,
                "Random pressure range is invalid.")
            .Validate(
                options =>
                    options.MinimumTemperatureCelsius <
                    options.MaximumTemperatureCelsius,
                "Random temperature range is invalid.")
            .ValidateOnStart();

        services.AddSingleton(TimeProvider.System);

        services.AddSingleton<TelemetryPublishingMetrics>();

        services.AddSingleton(CreateInitialSettingsState);

        services.AddSingleton<SimulationSettingsCommandApplier>();

        services.AddSingleton<ITelemetryReadingGenerator>(
            CreateReadingGenerator);

        services.AddHostedService<TelemetrySimulationWorker>();

        return services;
    }

    /// <summary>
    /// Creates the initial runtime simulation state.
    /// </summary>
    /// <param name="serviceProvider">
    /// Provides the validated simulation options.
    /// </param>
    /// <returns>
    /// State initialised from application configuration.
    /// </returns>
    private static SimulationSettingsState CreateInitialSettingsState(
        IServiceProvider serviceProvider)
    {
        SimulationOptions options = serviceProvider
            .GetRequiredService<IOptions<SimulationOptions>>()
            .Value;

        var initialSettings = new SimulationSettings(
            InitialSettingsRevision,
            options.DeviceIds,
            TimeSpan.FromMilliseconds(
                options.PublishingIntervalMilliseconds));

        return new SimulationSettingsState(initialSettings);
    }

    /// <summary>
    /// Creates the configured telemetry reading generator.
    /// </summary>
    /// <param name="serviceProvider">
    /// Provides the simulation options and time provider.
    /// </param>
    /// <returns>
    /// Generator matching the configured generation mode.
    /// </returns>
    private static ITelemetryReadingGenerator CreateReadingGenerator(
        IServiceProvider serviceProvider)
    {
        SimulationOptions options = serviceProvider
            .GetRequiredService<IOptions<SimulationOptions>>()
            .Value;

        TimeProvider timeProvider =
            serviceProvider.GetRequiredService<TimeProvider>();

        return options.GenerationMode switch
        {
            TelemetryGenerationMode.Fixed =>
                new FixedTelemetryReadingGenerator(
                    timeProvider,
                    options.FixedPressurePsi,
                    options.FixedTemperatureCelsius),

            TelemetryGenerationMode.Random =>
                new RandomTelemetryReadingGenerator(
                    timeProvider,
                    Random.Shared,
                    options.MinimumPressurePsi,
                    options.MaximumPressurePsi,
                    options.MinimumTemperatureCelsius,
                    options.MaximumTemperatureCelsius),

            _ => throw new InvalidOperationException(
                $"Unsupported generation mode " +
                $"'{options.GenerationMode}'.")
        };
    }
}
