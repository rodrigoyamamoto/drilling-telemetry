namespace DrillingTelemetry.Contracts.Commands;

/// <summary>
/// Requests an update to the running telemetry simulation.
/// </summary>
public sealed class UpdateSimulationSettingsCommand
{
    /// <summary>
    /// Gets or sets the monotonically increasing settings revision.
    /// </summary>
    public long Revision { get; set; }

    /// <summary>
    /// Gets or sets the devices included in each publishing cycle.
    /// </summary>
    public string[] DeviceIds { get; set; } = [];

    /// <summary>
    /// Gets or sets the interval between publishing cycles, in milliseconds.
    /// </summary>
    public int PublishingIntervalMilliseconds { get; set; }
}
