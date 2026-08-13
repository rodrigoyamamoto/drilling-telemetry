using System.Text.Json;
using DrillingTelemetry.Contracts;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

const string rabbitMqHostName = "localhost";
const string queueName = "drilling.telemetry.readings";

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

await channel.BasicQosAsync(
    prefetchSize: 0,
    prefetchCount: 1,
    global: false);

var consumer = new AsyncEventingBasicConsumer(channel);
consumer.ReceivedAsync += async (sender, eventArgs) =>
{
    var eventConsumer = (AsyncEventingBasicConsumer)sender;
    IChannel consumerChannel = eventConsumer.Channel;
    byte[] body = eventArgs.Body.ToArray();

    try
    {
        TelemetryReading? reading = JsonSerializer.Deserialize<TelemetryReading>(body);

        if (reading is null)
        {
            Console.WriteLine("The received message is empty");

            await consumerChannel.BasicNackAsync(
                deliveryTag: eventArgs.DeliveryTag,
                multiple: false,
                requeue: false);

            return;
        }

        Console.WriteLine("Telemetry reading received:");
        Console.WriteLine($"Device: {reading.DeviceId}");
        Console.WriteLine($"Pressure: {reading.PressurePsi} psi");
        Console.WriteLine($"Temperature: {reading.TemperatureCelsius} °C");
        Console.WriteLine($"Timestamp: {reading.TimestampUtc:O}");

        await consumerChannel.BasicAckAsync(
            deliveryTag: eventArgs.DeliveryTag,
            multiple: false);

        Console.WriteLine("Reading acknowledged.");
    }
    catch (JsonException exception)
    {
        Console.WriteLine($"Invalid telemetry message: {exception.Message}");

        await consumerChannel.BasicNackAsync(
            deliveryTag: eventArgs.DeliveryTag,
            multiple: false,
            requeue: false);
    }
};

string consumerTag = await channel.BasicConsumeAsync(
    queue: queueName,
    autoAck: false,
    consumer: consumer);

Console.WriteLine($"Waiting for messages from '{queueName}'.");
Console.WriteLine("Press Enter to stop.");

Console.ReadLine();

await channel.BasicCancelAsync(consumerTag);
