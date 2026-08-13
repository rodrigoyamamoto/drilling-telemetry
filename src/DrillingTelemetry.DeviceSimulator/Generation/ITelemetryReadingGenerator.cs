using DrillingTelemetry.Contracts;

namespace DrillingTelemetry.DeviceSimulator.Generation;

internal interface ITelemetryReadingGenerator
{
    /// <summary>
    /// Generates a telemetry reading for the specified device.
    /// </summary>
    /// <param name="deviceId">Identifier of the device.</param>
    /// <returns>The generated telemetry reading.</returns>
    TelemetryReading Generate(string deviceId);
}
