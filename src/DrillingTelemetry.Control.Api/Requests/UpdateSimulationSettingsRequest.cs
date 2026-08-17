using System.ComponentModel.DataAnnotations;

namespace DrillingTelemetry.Control.Api.Requests;

/// <summary>
/// Represents a request to update the running simulation.
/// </summary>
public sealed record UpdateSimulationSettingsRequest
{
    /// <summary>
    /// Gets the devices included in each publishing cycle.
    /// </summary>
    [Required(ErrorMessage = "At least one device must be provided.")]
    [MinLength(1, ErrorMessage = "At least one device must be provided.")]
    public string[] DeviceIds { get; init; } = [];

    /// <summary>
    /// Gets the interval between publishing cycles, in milliseconds.
    /// </summary>
    [Range(1, int.MaxValue, ErrorMessage = "Publishing interval must be greater than zero.")]
    public int PublishingIntervalMilliseconds { get; init; }
}
