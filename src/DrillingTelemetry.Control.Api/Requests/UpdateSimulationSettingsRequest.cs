namespace DrillingTelemetry.Control.Api.Requests;

/// <summary>
/// Represents a request to update the running simulation.
/// </summary>
public sealed record UpdateSimulationSettingsRequest
{
    /// <summary>
    /// Gets the monotonically increasing settings revision.
    /// </summary>
    public long Revision { get; init; }

    /// <summary>
    /// Gets the devices included in each publishing cycle.
    /// </summary>
    public string[]? DeviceIds { get; init; }

    /// <summary>
    /// Gets the interval between publishing cycles, in milliseconds.
    /// </summary>
    public int PublishingIntervalMilliseconds { get; init; }
}
