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
/// REL-006 — every queue has a dead-letter queue and a documented replay procedure. Redelivery
/// and retry are configured explicitly below rather than left to defaults, and the procedure is
/// written in docs/runbooks/catalog-messaging-replay.md.
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

                // REL-006 — a message that cannot be handled must come to rest somewhere a human
                // can find it, not disappear and not spin forever.
                //
                // Two tiers, because they answer different failures. Redelivery waits out a
                // transient outage (a database failing over, Promotion restarting) with gaps long
                // enough to matter. Immediate retry covers the momentary blip. Once both are
                // exhausted the message moves to <queue>_error — the dead-letter queue — and stays
                // there until someone replays it.
                //
                // REL-003's inbox is what makes replay safe: a replayed message is a duplicate,
                // and a duplicate produces exactly one effect.
                rabbit.UseDelayedRedelivery(r => r.Intervals(
                    TimeSpan.FromMinutes(1),
                    TimeSpan.FromMinutes(5),
                    TimeSpan.FromMinutes(15)));

                rabbit.UseMessageRetry(r => r.Immediate(3));

                rabbit.ConfigureEndpoints(context);
            });
        });

        return services;
    }
}
