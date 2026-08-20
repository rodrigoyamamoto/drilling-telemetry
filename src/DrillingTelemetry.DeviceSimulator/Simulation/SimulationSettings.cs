using DrillingTelemetry.Contracts;

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
    /// <param name="drillingOperation">
    /// Drilling operation applied between publishing cycles.
    /// </param>
    /// <param name="depthChangeRateMetresPerHour">
    /// Signed measured-depth change rate, in metres per hour.
    /// </param>
    public SimulationSettings(
        long revision,
        IReadOnlyList<string> deviceIds,
        TimeSpan publishingInterval,
        DrillingOperation drillingOperation,
        double depthChangeRateMetresPerHour)
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

        if (deviceIds.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException(
                "Device identifiers must not be empty.",
                nameof(deviceIds));
        }

        TimeSpan minimumPublishingInterval =
            TimeSpan.FromMilliseconds(
                SimulationLimits
                    .MinimumPublishingIntervalMilliseconds);

        if (publishingInterval < minimumPublishingInterval)
        {
            throw new ArgumentOutOfRangeException(
                nameof(publishingInterval),
                publishingInterval,
                "The publishing interval must be at least " +
                $"{SimulationLimits.MinimumPublishingIntervalMilliseconds} " +
                "milliseconds.");
        }

        if (!Enum.IsDefined(drillingOperation))
        {
            throw new ArgumentOutOfRangeException(
                nameof(drillingOperation),
                drillingOperation,
                "The drilling operation is invalid.");
        }

        if (!DrillingOperationValidation.IsValid(
                drillingOperation,
                depthChangeRateMetresPerHour))
        {
            throw new ArgumentException(
                "Drilling ahead requires a positive depth-change rate, " +
                "stationary requires zero, and tripping out requires a " +
                "negative rate.",
                nameof(depthChangeRateMetresPerHour));
        }

        Revision = revision;
        DeviceIds = Array.AsReadOnly(deviceIds.ToArray());
        PublishingInterval = publishingInterval;
        DrillingOperation = drillingOperation;
        DepthChangeRateMetresPerHour = depthChangeRateMetresPerHour;
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

    /// <summary>
    /// Gets the drilling operation applied between publishing cycles.
    /// </summary>
    public DrillingOperation DrillingOperation { get; }

    /// <summary>
    /// Gets the signed measured-depth change rate, in metres per hour.
    /// </summary>
    public double DepthChangeRateMetresPerHour { get; }
}
