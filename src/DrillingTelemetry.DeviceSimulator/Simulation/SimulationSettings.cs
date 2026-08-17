namespace DrillingTelemetry.DeviceSimulator.Simulation;

/// <summary>
/// Represents an immutable snapshot of the simulation settings.
/// </summary>
internal sealed class SimulationSettings
{
    /// <summary>
    /// Initialises a simulation settings snapshot.
    /// </summary>
    /// <param name="revision">
    /// Monotonically increasing settings revision.
    /// </param>
    /// <param name="deviceIds">
    /// Devices included in each publishing cycle.
    /// </param>
    /// <param name="publishingInterval">
    /// Time waited between publishing cycles.
    /// </param>
    public SimulationSettings(
        long revision,
        IReadOnlyList<string> deviceIds,
        TimeSpan publishingInterval)
    {
        ArgumentNullException.ThrowIfNull(deviceIds);

        if (revision <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(revision),
                revision,
                "The settings revision must be greater than zero.");
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

        Revision = revision;
        DeviceIds = Array.AsReadOnly(deviceIds.ToArray());
        PublishingInterval = publishingInterval;
    }

    /// <summary>
    /// Gets the settings revision.
    /// </summary>
    public long Revision { get; }

    /// <summary>
    /// Gets the devices included in each publishing cycle.
    /// </summary>
    public IReadOnlyList<string> DeviceIds { get; }

    /// <summary>
    /// Gets the time waited between publishing cycles.
    /// </summary>
    public TimeSpan PublishingInterval { get; }
}
