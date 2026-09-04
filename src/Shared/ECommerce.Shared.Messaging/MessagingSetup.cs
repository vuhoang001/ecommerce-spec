using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerce.Shared.Messaging;

/// <summary>
/// MassTransit over RabbitMQ, with the EF Core inbox in the consuming module's schema.
/// </summary>
/// <remarks>
/// REL-003 — delivery is at-least-once; deduplication is the consumer's obligation and is
/// implemented by <see cref="Inbox.InboxDeduplicator"/> against the module's own inbox table.
/// REL-006 — every queue gets a dead-letter queue; MassTransit's error queue is that, and the
/// replay procedure lives in the module runbook.
/// </remarks>
public static class MessagingSetup
{
    public static IServiceCollection AddCatalogMessaging<TDbContext>(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<IBusRegistrationConfigurator>? configureConsumers = null)
        where TDbContext : DbContext
    {
        services.AddMassTransit(bus =>
        {
            configureConsumers?.Invoke(bus);

            bus.AddEntityFrameworkOutbox<TDbContext>(outbox =>
            {
                outbox.UsePostgres();

                // REL-002 — the relay claims rows with FOR UPDATE SKIP LOCKED so concurrent
                // relay instances never publish the same row twice. Verified by capturing the
                // emitted SQL (research.md R7), not trusted from documentation.
                outbox.UseBusOutbox();
            });

            bus.UsingRabbitMq((context, rabbit) =>
            {
                var host = configuration.GetConnectionString("RabbitMq")
                           ?? "amqp://guest:guest@localhost:5672";
                rabbit.Host(new Uri(host));

                // REL-005 — tolerant readers: an unknown field must be ignored, never rejected.
                rabbit.UseRawJsonDeserializer(isDefault: false);

                rabbit.ConfigureEndpoints(context);
            });
        });

        return services;
    }
}
