using Confluent.Kafka;
using KafkaFlow;
using KafkaFlow.Configuration;
using KafkaFlow.Serializer;
using KafkaFlow.TypedHandler;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;

namespace Shared;

public static class DI
{
    public static void AddMyAuthorization(this IServiceCollection services)
    {
        services.AddAuthorization();
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(opt =>
        {
            opt.TokenValidationParameters = new()
            {
                ValidateIssuer = false,
                ValidateAudience = false,
                ValidateLifetime = false,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = TokenService.GetSymmetricSecurityKey()
            };
        });
    }

    public static void AddSwagger(this IServiceCollection services)
    {
        var securityScheme = new OpenApiSecurityScheme()
        {
            Name = "Authorization",
            Type = SecuritySchemeType.ApiKey,
            Scheme = "Bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "JSON Web Token based security",
        };

        var securityReq = new OpenApiSecurityRequirement()
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = "Bearer"
                    }
                },
                Array.Empty<string>()
            }
        };

        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(o =>
        {
            o.AddSecurityDefinition("Bearer", securityScheme);
            o.AddSecurityRequirement(securityReq);
        });
    }

    public static void UseMyAuthorization(this IApplicationBuilder app)
    {
        app.UseAuthentication();
        app.UseAuthorization();
    }

    public static void UseSwagger(this IApplicationBuilder app)
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

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