using ECommerce.Shared.Messaging.Envelope;
using FluentAssertions;

namespace ECommerce.Shared.Messaging.Tests;

/// <summary>
/// COM-006 — a message missing any of message_id, type, version, occurred_at, correlation_id
/// or causation_id is rejected at the transport boundary.
/// </summary>
public class EnvelopeValidationTests
{
    private static MessageEnvelope Valid() => new(
        Guid.NewGuid(), "promotion.discount.changed.v1", 1,
        new DateTimeOffset(2026, 9, 4, 12, 0, 0, TimeSpan.Zero), Guid.NewGuid(), Guid.NewGuid());

    [Fact]
    public void A_complete_envelope_is_accepted()
    {
        EnvelopeValidator.IsValid(Valid()).Should().BeTrue();
    }

    [Fact]
    public void A_missing_message_id_is_rejected_because_deduplication_depends_on_it()
    {
        var envelope = Valid() with { MessageId = Guid.Empty };
        EnvelopeValidator.Validate(envelope).Should().ContainSingle()
            .Which.Field.Should().Be("message_id");
    }

    [Theory]
    [InlineData("type")]
    [InlineData("version")]
    [InlineData("occurred_at")]
    [InlineData("correlation_id")]
    [InlineData("causation_id")]
    public void Every_required_field_is_checked(string field)
    {
        var envelope = field switch
        {
            "type" => Valid() with { Type = "  " },
            "version" => Valid() with { Version = 0 },
            "occurred_at" => Valid() with { OccurredAt = default },
            "correlation_id" => Valid() with { CorrelationId = Guid.Empty },
            _ => Valid() with { CausationId = Guid.Empty }
        };

        EnvelopeValidator.Validate(envelope).Select(f => f.Field).Should().Contain(field);
    }

    [Fact]
    public void A_null_envelope_is_rejected_rather_than_crashing()
    {
        EnvelopeValidator.Validate(null).Should().ContainSingle();
    }

    [Fact]
    public void Rejection_names_every_missing_field_at_once()
    {
        var envelope = new MessageEnvelope(Guid.Empty, "", 0, default, Guid.Empty, Guid.Empty);
        var act = () => EnvelopeValidator.ThrowIfInvalid(envelope);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*COM-006*")
            .And.Message.Should().Contain("message_id").And.Contain("causation_id");
    }
}
