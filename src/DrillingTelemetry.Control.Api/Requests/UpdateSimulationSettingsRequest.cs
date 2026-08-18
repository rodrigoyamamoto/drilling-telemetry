using System.ComponentModel.DataAnnotations;

namespace DrillingTelemetry.Control.Api.Requests;

/// <summary>
/// Represents a request to update the running simulation.
/// </summary>
public sealed record UpdateSimulationSettingsRequest : IValidatableObject
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

    /// <summary>
    /// Validates rules that depend on the request contents.
    /// </summary>
    /// <param name="validationContext">
    /// Context in which the request is being validated.
    /// </param>
    /// <returns>
    /// Validation errors found in the request.
    /// </returns>
    public IEnumerable<ValidationResult> Validate(
        ValidationContext validationContext)
    {
        ArgumentNullException.ThrowIfNull(validationContext);

        if (DeviceIds.Any(string.IsNullOrWhiteSpace))
        {
            yield return new ValidationResult(
                "Device identifiers must not be empty.",
                [nameof(DeviceIds)]);
        }
    }
}
