using Confluent.Kafka;
using KafkaFlow;
using KafkaFlow.Configuration;
using KafkaFlow.Serializer;
using KafkaFlow.TypedHandler;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;

namespace Shared;

public static class Kafka
{
    public static IClusterConfigurationBuilder AddProducer(this IClusterConfigurationBuilder builder, string name, string topic)
    {
        return builder.AddProducer(name, producer => producer
            .DefaultTopic(topic)
            .WithProducerConfig(
                new ProducerConfig
                {
                    SocketTimeoutMs = 60000,
                    SocketKeepaliveEnable = true,
                    MetadataMaxAgeMs = 180000,
                    ConnectionsMaxIdleMs = 180000,
                    SocketNagleDisable = true,
                    Acks = Confluent.Kafka.Acks.All
                })
            .AddMiddlewares(x => x.AddSerializer<JsonCoreSerializer>()));
    }

    public static IClusterConfigurationBuilder AddConsumer(this IClusterConfigurationBuilder builder, string topic, string groupId, params Type[] handlers)
    {
        return builder.AddConsumer(consumer => consumer
            .Topic(topic)
            .WithGroupId(groupId)
            .WithBufferSize(1)
            .WithWorkersCount(1)
            .WithConsumerConfig(
                new ConsumerConfig
                {
                    SocketTimeoutMs = 60000,
                    SocketKeepaliveEnable = true,
                    MetadataMaxAgeMs = 180000,
                    ConnectionsMaxIdleMs = 180000,
                    MaxPollIntervalMs = 500000,
                    SessionTimeoutMs = 30000,
                    SocketNagleDisable = true,
                    AllowAutoCreateTopics = true,
                })
            .AddMiddlewares(x => x
                .AddSerializer<JsonCoreSerializer>()
                .AddTypedHandlers(x => x.AddHandlers(handlers))));
    }

    public static IKafkaBus UseKafka(this IApplicationBuilder app, IHostApplicationLifetime hostApplicationLifetime)
    {
        var kafkaBus = app.ApplicationServices.CreateKafkaBus();

        hostApplicationLifetime.ApplicationStarted.Register(() => kafkaBus.StartAsync(hostApplicationLifetime.ApplicationStopped));

        return kafkaBus;
    }
}