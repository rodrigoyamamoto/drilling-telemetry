using System.Text.Json.Serialization;

namespace DrillingTelemetry.Contracts;

/// <summary>
/// Describes the current movement of the drilling assembly along the
/// wellbore.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<DrillingOperation>))]
public enum DrillingOperation
{
    /// <summary>
    /// The assembly is not moving along the wellbore.
    /// </summary>
    Stationary,

    /// <summary>
    /// The wellbore is being extended and measured depth is increasing.
    /// </summary>
    DrillingAhead,

    /// <summary>
    /// The assembly is being withdrawn and measured depth is decreasing.
    /// </summary>
    TrippingOut
}
