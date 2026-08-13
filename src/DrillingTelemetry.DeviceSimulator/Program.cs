using System.Text;
using System.Text.Json;
using DrillingTelemetry.Contracts;
using DrillingTelemetry.DeviceSimulator;
using RabbitMQ.Client;

const string rabbitMqHostName = "localhost";
const string queueName = "drilling.telemetry.readings";

const string fixedGenerationMode = "fixed";
const string randomGenerationMode = "random";

const double fixedPressurePsi = 8250;
const double fixedTemperatureCelsius = 117.5;

const double minimumPressurePsi = 7000;
const double maximumPressurePsi = 9000;
const double minimumTemperatureCelsius = 100;
const double maximumTemperatureCelsius = 140;

string[] deviceIds =
[
    "DRILL-001",
    "DRILL-002",
    "DRILL-003"
];

string generationMode = args.Length > 0
    ? args[0].ToLowerInvariant()
    : fixedGenerationMode;

ITelemetryReadingGenerator? readingGenerator = generationMode switch
{
    fixedGenerationMode => new FixedTelemetryReadingGenerator(
        TimeProvider.System,
        fixedPressurePsi,
        fixedTemperatureCelsius),

    randomGenerationMode => new RandomTelemetryReadingGenerator(TimeProvider.System,
        Random.Shared,
        minimumPressurePsi,
        maximumPressurePsi,
        minimumTemperatureCelsius,
        maximumTemperatureCelsius),

    _ => null,
};

if (readingGenerator is null)
{
    Console.WriteLine($"Unknown generation mode '{generationMode}'.");
    Console.WriteLine(
        $"Available modes: {fixedGenerationMode}, {randomGenerationMode}.");

    return;
}

Console.WriteLine($"Using '{generationMode}' generation mode.");

var connectionFactory = new ConnectionFactory()
{
    HostName = rabbitMqHostName
};

await using IConnection connection = await connectionFactory.CreateConnectionAsync();
await using IChannel channel = await connection.CreateChannelAsync();

await channel.QueueDeclareAsync(
    queue: queueName,
    durable: true,
    exclusive: false,
    autoDelete: false,
    arguments: null);

var properties = new BasicProperties
{
    ContentType = "application/json", Persistent = true
};

foreach (string deviceId in deviceIds)
{
    TelemetryReading reading = readingGenerator.Generate(deviceId);
    string message = JsonSerializer.Serialize(reading);
    byte[] body = Encoding.UTF8.GetBytes(message);

    await channel.BasicPublishAsync(
        exchange: string.Empty,
        routingKey: queueName,
        mandatory: true,
        basicProperties: properties,
        body: body);

    Console.WriteLine($"Reading published to '{queueName}':");
    Console.WriteLine(message);
}
