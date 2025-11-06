using Challenge.Api.Controllers;
using Confluent.Kafka;
using System.Text.Json;

namespace Challenge.Api.Kafka;

public sealed class KafkaConsumerHostedService : BackgroundService
{
    private readonly ILogger<KafkaConsumerHostedService> _log;
    private readonly IProcessedEventStore _store;
    private readonly IConsumer<Ignore, string> _consumer;
    private readonly string _topic;

    public KafkaConsumerHostedService(
        ILogger<KafkaConsumerHostedService> log,
        IProcessedEventStore store,
        IConfiguration config)
    {
        _log = log;
        _store = store;

        var cfg = new ConsumerConfig
        {
            BootstrapServers = config["Kafka:BootstrapServers"],
            GroupId = config["Kafka:GroupId"] ?? "challenge-consumer",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = true
        };

        _consumer = new ConsumerBuilder<Ignore, string>(cfg).Build();
        _topic = config["Kafka:Topic"] ?? "user-events";
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _consumer.Subscribe(_topic);
        _log.LogInformation("Kafka consumer started listening on topic {Topic}", _topic);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var result = _consumer.Consume(stoppingToken);
                    if (result?.Message?.Value is null) continue;

                    var evt = JsonSerializer.Deserialize<UserEvent>(result.Message.Value);
                    if (evt is not null)
                    {
                        _store.Append(evt);
                        _log.LogInformation("Processed event from Kafka userId={UserId} type={Type}", evt.UserId, evt.Type);
                    }
                }
                catch (ConsumeException ex)
                {
                    _log.LogError(ex, "Kafka consume error");
                }
                await Task.Delay(10, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            _log.LogInformation("Kafka consumer stopping...");
        }
        finally
        {
            _consumer.Close();
            _consumer.Dispose();
        }
    }
}
