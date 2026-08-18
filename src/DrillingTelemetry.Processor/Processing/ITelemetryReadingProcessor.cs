using DrillingTelemetry.Contracts;

namespace DrillingTelemetry.Processor.Processing;

/// <summary>
/// Applies the processing policy for a valid telemetry reading.
/// </summary>
internal interface ITelemetryReadingProcessor
{
    /// <summary>
    /// Processes a telemetry reading and publishes it when it advances the
    /// device sequence.
    /// </summary>
    /// <param name="reading">Valid telemetry reading to process.</param>
    /// <param name="cancellationToken">
    /// Signals that processing should be cancelled.
    /// </param>
    Task ProcessAsync(
        TelemetryReading reading,
        CancellationToken cancellationToken);
}
