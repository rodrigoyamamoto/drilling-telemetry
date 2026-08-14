namespace DrillingTelemetry.DeviceSimulator.Simulation;

/// <summary>
/// Represents an immutable snapshot of the simulation configuration.
/// </summary>
internal sealed class SimulationSettings
{
    /// <summary>
    /// Initializes a simulation configuration snapshot.
    /// </summary>
    /// <param name="version">
    /// Monotonically increasing configuration version.
    /// </param>
    /// <param name="deviceIds">
    /// Devices included in each publishing cycle.
    /// </param>
    /// <param name="publishingInterval">
    /// Time waited between publishing cycles.
    /// </param>
    public SimulationSettings(
        long version,
        IReadOnlyList<string> deviceIds,
        TimeSpan publishingInterval)
    {
        ArgumentNullException.ThrowIfNull(deviceIds);

        if (version <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(version),
                version,
                "The settings version must be greater than zero.");
        }

        if (deviceIds.Count == 0)
        {
            throw new ArgumentException(
                "At least one device must be configured.",
                nameof(deviceIds));
        }

        if (publishingInterval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(publishingInterval),
                publishingInterval,
                "The publishing interval must be greater than zero.");
        }

        Version = version;
        DeviceIds = Array.AsReadOnly(deviceIds.ToArray());
        PublishingInterval = publishingInterval;
    }

    /// <summary>
    /// Gets the configuration version.
    /// </summary>
    public long Version { get; }

    /// <summary>
    /// Gets the devices included in each publishing cycle.
    /// </summary>
    public IReadOnlyList<string> DeviceIds { get; }

    /// <summary>
    /// Gets the time waited between publishing cycles.
    /// </summary>
    public TimeSpan PublishingInterval { get; }
}