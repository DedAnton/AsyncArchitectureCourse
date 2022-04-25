using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using KafkaFlow;
using KafkaFlow.Serializer;
using KafkaFlow.TypedHandler;
using Microsoft.Extensions.DependencyInjection;
using KafkaFlow.Configuration;
using Confluent.Kafka;

namespace Shared;

public static class Kafka
{
    public static IClusterConfigurationBuilder AddKafka(this IServiceCollection services, string broker)
    {
        IClusterConfigurationBuilder? clusterBuilder = null;

        services.AddKafka(kafka =>
        {
            kafka.UseConsoleLog();
            kafka.AddCluster(cluster =>
            {
                cluster.WithBrokers(new[] { broker });
                cluster.WithSecurityInformation(information =>
                {
                    information.SaslMechanism = KafkaFlow.Configuration.SaslMechanism.Plain;
                    information.SaslUsername = "$ConnectionString";
                    information.SecurityProtocol = KafkaFlow.Configuration.SecurityProtocol.SaslSsl;
                });

                clusterBuilder = cluster;
            });
        });

        return clusterBuilder!;
    }

    public static TypedHandlerConfigurationBuilder AddConsumer(this IClusterConfigurationBuilder builder, string topic, string groupId)
    {
        TypedHandlerConfigurationBuilder? typedHandlerBuilder = null;

        builder
            .AddConsumer(consumer =>
            {
                consumer
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
                        })
                    .AddMiddlewares(middlewares =>
                    {
                        middlewares.AddSerializer<JsonCoreSerializer>();
                        middlewares.AddTypedHandlers(x =>
                        {
                            typedHandlerBuilder = x;
                        });
                    });
            });

        return typedHandlerBuilder!;
    }

    public static IKafkaBus UseKafka(this IServiceProvider serviceProvider, IHostApplicationLifetime hostApplicationLifetime)
    {
        var kafkaBus = serviceProvider.CreateKafkaBus();

        hostApplicationLifetime.ApplicationStarted.Register(() => kafkaBus.StartAsync(hostApplicationLifetime.ApplicationStopped));

        return kafkaBus;
    }
}