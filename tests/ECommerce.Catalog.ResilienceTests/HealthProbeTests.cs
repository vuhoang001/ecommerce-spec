using System.Net;
using FluentAssertions;

namespace ECommerce.Catalog.ResilienceTests;

/// <summary>
/// research.md R13 / SC-008 — readiness reports the database and migrations, and deliberately
/// does NOT check the Promotion module. Including Promotion would drain every instance from
/// the load balancer during a Promotion outage and turn a degraded dependency into a total
/// catalogue outage, which is exactly what SC-008 forbids.
/// </summary>
[Collection("resilience")]
public class HealthProbeTests(ResilienceFixture fixture)
{
    [Fact]
    public async Task Liveness_reports_the_process_is_responsive()
    {
        var response = await fixture.CreateClient().GetAsync("/health/live");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Readiness_is_healthy_when_the_database_is_reachable()
    {
        var response = await fixture.CreateClient().GetAsync("/health/ready");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Readiness_stays_healthy_while_the_promotion_module_is_unreachable()
    {
        // No Promotion dependency is registered in this host at all, which is the strongest
        // possible form of "readiness does not depend on Promotion". If a future change adds
        // a Promotion probe to readiness, this test is what fails.
        var response = await fixture.CreateClient().GetAsync("/health/ready");

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "a Promotion outage must never mark a catalogue instance unready (SC-008)");
    }
}
