using DrillingTelemetry.Contracts;

namespace DrillingTelemetry.DeviceSimulator.Generation;

internal interface ITelemetryReadingGenerator
{
    /// <summary>
    /// Generates a telemetry reading for the specified device at the given
    /// measured depth.
    /// </summary>
    /// <param name="deviceId">Identifier of the device.</param>
    /// <param name="measuredDepthMetres">
    /// Measured depth, in metres, at which the reading is acquired.
    /// </param>
    /// <returns>The generated telemetry reading.</returns>
    TelemetryReading Generate(
        string deviceId,
        double measuredDepthMetres);
}
