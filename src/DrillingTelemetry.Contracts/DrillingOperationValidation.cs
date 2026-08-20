namespace DrillingTelemetry.Contracts;

/// <summary>
/// Validates the relationship between a drilling operation and its signed
/// measured-depth change rate.
/// </summary>
public static class DrillingOperationValidation
{
    /// <summary>
    /// Determines whether an operation and depth-change rate are coherent.
    /// </summary>
    /// <param name="operation">Drilling operation being validated.</param>
    /// <param name="depthChangeRateMetresPerHour">
    /// Signed change in measured depth per hour.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when the operation is defined and the rate has
    /// the required sign; otherwise, <see langword="false"/>.
    /// </returns>
    public static bool IsValid(
        DrillingOperation operation,
        double depthChangeRateMetresPerHour)
    {
        if (!double.IsFinite(depthChangeRateMetresPerHour))
        {
            return false;
        }

        return operation switch
        {
            DrillingOperation.Stationary =>
                depthChangeRateMetresPerHour == 0,
            DrillingOperation.DrillingAhead =>
                depthChangeRateMetresPerHour > 0,
            DrillingOperation.TrippingOut =>
                depthChangeRateMetresPerHour < 0,
            _ => false
        };
    }
}
