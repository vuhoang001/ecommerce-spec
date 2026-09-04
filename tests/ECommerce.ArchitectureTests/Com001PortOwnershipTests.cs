using System.Reflection;
using ECommerce.Catalog.Application.Ports;
using FluentAssertions;

namespace ECommerce.ArchitectureTests;

/// <summary>
/// COM-001 — a cross-module read goes through a proto-defined contract and a port interface
/// owned by the CONSUMER, implemented outside its domain.
/// </summary>
public class Com001PortOwnershipTests
{
    [Fact]
    public void The_port_is_declared_in_the_consuming_modules_application_assembly()
    {
        typeof(IPromotionPricingPort).Assembly.GetName().Name
            .Should().Be("ECommerce.Catalog.Application",
                "COM-001 puts ownership of the port with the consumer, not the provider");
    }

    [Fact]
    public void The_port_is_implemented_outside_the_consuming_modules_domain()
    {
        var implementations = ModuleAssemblies.All()
            .Where(a => !a.GetName().Name!.EndsWith("Tests", StringComparison.Ordinal))
            .SelectMany(a => a.GetTypes())
            .Where(t => t is { IsClass: true, IsAbstract: false }
                        && typeof(IPromotionPricingPort).IsAssignableFrom(t))
            .ToList();

        implementations.Should().NotBeEmpty("the port needs an implementation to be usable");
        implementations.Should().OnlyContain(
            t => !t.Assembly.GetName().Name!.EndsWith(".Domain", StringComparison.Ordinal),
            "COM-001 keeps the implementation out of the domain");
    }

    [Fact]
    public void The_port_speaks_the_proto_defined_contract()
    {
        // The contract is the generated proto type, not a hand-rolled DTO — that is what makes
        // the gRPC client a drop-in replacement after extraction (research.md R5).
        var method = typeof(IPromotionPricingPort).GetMethod(nameof(IPromotionPricingPort.GetPricingAsync))!;
        var returned = method.ReturnType.GetGenericArguments().Single();

        returned.Assembly.GetName().Name
            .Should().Be("ECommerce.Promotion.Contracts");
    }
}
