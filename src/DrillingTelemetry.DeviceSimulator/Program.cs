using System.Text;
using System.Text.Json;
using DrillingTelemetry.Contracts;
using DrillingTelemetry.DeviceSimulator;
using RabbitMQ.Client;

const string rabbitMqHostName = "localhost";
const string queueName = "drilling.telemetry.readings";
const double pressurePsi = 8_250;
const double temperatureCelsius = 117.5;

string[] deviceIds =
[
    "DRILL-001",
    "DRILL-002",
    "DRILL-003"
];

var readingGenerator = new FixedTelemetryReadingGenerator(
    TimeProvider.System,
    pressurePsi,
    temperatureCelsius
);

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
