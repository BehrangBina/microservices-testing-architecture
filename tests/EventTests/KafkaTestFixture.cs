using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Xunit;

namespace EventTests;

/// <summary>
/// Shared fixture that provides isolated Kafka producer/consumer instances per test run.
///
/// Each test run uses a unique topic prefix (test-{guid}) to prevent cross-contamination
/// between parallel test executions or leftover messages from previous runs.
///
/// Requires Kafka running at KAFKA_BOOTSTRAP_SERVERS (default: localhost:9094).
/// In CI this is provided by docker-compose service 'kafka' exposed on port 9094.
/// </summary>
public class KafkaTestFixture : IAsyncLifetime
{
    public string BootstrapServers { get; } =
        Environment.GetEnvironmentVariable("KAFKA_BOOTSTRAP_SERVERS") ?? "localhost:9094";

    public string TopicPrefix { get; } = $"test-{Guid.NewGuid():N}";

    public string OrderCreatedTopic => $"{TopicPrefix}-order-created";
    public string PaymentProcessedTopic => $"{TopicPrefix}-payment-processed";

    private IAdminClient? _adminClient;

    public async Task InitializeAsync()
    {
        _adminClient = new AdminClientBuilder(new AdminClientConfig
        {
            BootstrapServers = BootstrapServers
        }).Build();

        var topicSpecs = new[]
        {
            new TopicSpecification { Name = OrderCreatedTopic,    NumPartitions = 1, ReplicationFactor = 1 },
            new TopicSpecification { Name = PaymentProcessedTopic, NumPartitions = 1, ReplicationFactor = 1 }
        };

        await _adminClient.CreateTopicsAsync(topicSpecs);
    }

    public IProducer<string, string> CreateProducer() =>
        new ProducerBuilder<string, string>(new ProducerConfig
        {
            BootstrapServers = BootstrapServers,
            Acks = Acks.All
        }).Build();

    public IConsumer<string, string> CreateConsumer(string groupId) =>
        new ConsumerBuilder<string, string>(new ConsumerConfig
        {
            BootstrapServers = BootstrapServers,
            GroupId = groupId,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = true
        }).Build();

    public async Task DisposeAsync()
    {
        if (_adminClient is not null)
        {
            try
            {
                await _adminClient.DeleteTopicsAsync(new[] { OrderCreatedTopic, PaymentProcessedTopic });
            }
            catch { /* best-effort cleanup */ }
            _adminClient.Dispose();
        }
    }
}
