using System.ComponentModel.DataAnnotations;
using DrillingTelemetry.Contracts;

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
    [Range(
        SimulationLimits.MinimumPublishingIntervalMilliseconds,
        int.MaxValue,
        ErrorMessage =
            "Publishing interval must be at least 50 milliseconds.")]
    public int PublishingIntervalMilliseconds { get; init; }

    /// <summary>
    /// Gets the drilling operation applied between publishing cycles.
    /// </summary>
    public DrillingOperation DrillingOperation { get; init; }

    /// <summary>
    /// Gets the signed measured-depth change rate, in metres per hour.
    /// </summary>
    public double DepthChangeRateMetresPerHour { get; init; }

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

        if (!Enum.IsDefined(DrillingOperation))
        {
            yield return new ValidationResult(
                "Drilling operation is invalid.",
                [nameof(DrillingOperation)]);

            yield break;
        }

        if (!DrillingOperationValidation.IsValid(
                DrillingOperation,
                DepthChangeRateMetresPerHour))
        {
            yield return new ValidationResult(
                "Drilling ahead requires a positive depth-change rate, " +
                "stationary requires zero, and tripping out requires a " +
                "negative rate.",
                [
                    nameof(DrillingOperation),
                    nameof(DepthChangeRateMetresPerHour)
                ]);
        }
    }
}
