using DrillingTelemetry.DeviceSimulator.Generation;

namespace DrillingTelemetry.DeviceSimulator.Configuration;

/// <summary>
/// Contains the initial telemetry simulation configuration.
/// </summary>
internal sealed class SimulationOptions
{
    /// <summary>
    /// Configuration section containing the simulation settings.
    /// </summary>
    public const string SectionName = "Simulation";

    /// <summary>
    /// Gets or sets the initial telemetry generation mode.
    /// </summary>
    public TelemetryGenerationMode GenerationMode { get; set; } =
        TelemetryGenerationMode.Fixed;

    /// <summary>
    /// Gets or sets the initial device identifiers.
    /// </summary>
    public string[] DeviceIds { get; set; } = [];

    /// <summary>
    /// Gets or sets the initial publishing interval in milliseconds.
    /// </summary>
    public int PublishingIntervalMilliseconds { get; set; }

    /// <summary>
    /// Gets or sets the pressure used by fixed generation.
    /// </summary>
    public double FixedPressurePsi { get; set; }

    /// <summary>
    /// Gets or sets the temperature used by fixed generation.
    /// </summary>
    public double FixedTemperatureCelsius { get; set; }

    /// <summary>
    /// Gets or sets the minimum pressure used by random generation.
    /// </summary>
    public double MinimumPressurePsi { get; set; }

    /// <summary>
    /// Gets or sets the maximum pressure used by random generation.
    /// </summary>
    public double MaximumPressurePsi { get; set; }

    /// <summary>
    /// Gets or sets the minimum temperature used by random generation.
    /// </summary>
    public double MinimumTemperatureCelsius { get; set; }

    /// <summary>
    /// Gets or sets the maximum temperature used by random generation.
    /// </summary>
    public double MaximumTemperatureCelsius { get; set; }
}
