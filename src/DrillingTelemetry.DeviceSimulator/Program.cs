using System.Net.Mime;
using System.Text;
using System.Text.Json;
using DrillingTelemetry.DeviceSimulator;
using RabbitMQ.Client;

const string rabbitMqHostName = "localhost";
const string queueName = "drilling.telemetry.readings";

var reading = new TelemetryReading()
{
    DeviceId = "DRILL-001", PressurePsi = 8250, TemperatureCelsius = 117.5, TimestampUtc = DateTimeOffset.UtcNow
};

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

string message = JsonSerializer.Serialize(reading);
byte[] body = Encoding.UTF8.GetBytes(message);

var properties = new BasicProperties
{
    ContentType = "application/json", Persistent = true
};

await channel.BasicPublishAsync(
    exchange: string.Empty,
    routingKey: queueName,
    mandatory: true,
    basicProperties: properties,
    body: body);

Console.WriteLine($"Reading published to '{queueName}':");
Console.WriteLine(message);
