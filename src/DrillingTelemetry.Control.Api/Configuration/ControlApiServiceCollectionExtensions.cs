using DrillingTelemetry.Control.Api.Publishing;
using DrillingTelemetry.Control.Api.RuntimeSettings;

namespace DrillingTelemetry.Control.Api.Configuration;

/// <summary>
/// Provides dependency injection configuration for the Control API.
/// </summary>
internal static class ControlApiServiceCollectionExtensions
{
    private const string AllowedOriginsSectionName =
        "Cors:AllowedOrigins";

    /// <summary>
    /// Adds the Control API services to the application.
    /// </summary>
    /// <param name="services">
    /// Collection receiving the Control API services.
    /// </param>
    /// <param name="configuration">
    /// Provides the application configuration.
    /// </param>
    /// <returns>
    /// The same service collection so registrations can be chained.
    /// </returns>
    public static IServiceCollection AddControlApi(
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

        services.AddProblemDetails();
        services.AddOpenApi();
        services.AddValidation();

        services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
            {
                policy
                    .WithOrigins(allowedOrigins)
                    .AllowAnyHeader()
                    .WithMethods(HttpMethods.Post);
            });
        });

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
                        options.SimulationSettingsExchangeName),
                "RabbitMQ simulation settings exchange name is missing.")
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
