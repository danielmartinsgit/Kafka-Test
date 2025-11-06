using Challenge.Api.Controllers;
using Confluent.Kafka;
using System.Text.Json;

namespace Challenge.Api.Kafka;

public sealed class KafkaProducer : IEventProducer, IDisposable
{
    private readonly ILogger<KafkaProducer> _log;
    private readonly IProducer<Null, string> _producer;
    private readonly string _topic;

    public KafkaProducer(IConfiguration config, ILogger<KafkaProducer> log)
    {
        _log = log;

        var cfg = new ProducerConfig
        {
            BootstrapServers = config["Kafka:BootstrapServers"],
            Acks = Acks.All,
            LingerMs = 5,
            EnableIdempotence = true,
            MessageTimeoutMs = 30000
        };

        _topic = config["Kafka:Topic"] ?? "user-events";
        _producer = new ProducerBuilder<Null, string>(cfg).Build();
    }

    public async Task PublishAsync(UserEvent evt, CancellationToken ct)
    {
        var payload = JsonSerializer.Serialize(evt);

        try
        {
            var delivery = await _producer.ProduceAsync(_topic, new Message<Null, string> { Value = payload }, ct);
            _log.LogInformation("Produced event to {Topic} [partition={Partition}, offset={Offset}]",
                delivery.Topic, delivery.Partition.Value, delivery.Offset.Value);
        }
        catch (ProduceException<Null, string> ex)
        {
            _log.LogError(ex, "Failed to publish event to Kafka topic {Topic}", _topic);
            throw;
        }
    }

    public void Dispose()
    {
        try
        {
            _producer.Flush(TimeSpan.FromSeconds(3));
            _producer.Dispose();
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Error disposing Kafka producer");
        }
    }
}
