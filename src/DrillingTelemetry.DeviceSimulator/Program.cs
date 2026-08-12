using DrillingTelemetry.DeviceSimulator;

var reading = new TelemetryReading()
{
    DeviceId = "DRILL-001",
    PressurePsi = 8250,
    TemperatureCelsius = 117.5,
    TimestampUtc = DateTimeOffset.UtcNow
};

Console.WriteLine("Telemetry reading generated:");
Console.WriteLine($"Device: {reading.DeviceId}");
Console.WriteLine($"Pressure: {reading.PressurePsi} psi");
Console.WriteLine($"Temperature: {reading.TemperatureCelsius} °C");
Console.WriteLine($"Timestamp: {reading.TimestampUtc:O}");
