using System.Reflection;
using ECommerce.Catalog.Application.Ports;
using FluentAssertions;

namespace ECommerce.ArchitectureTests;

/// <summary>
/// PRM-001 [not adopted — see architecture-burndown.md BD-005] (NON-NEGOTIABLE) — the promotion module calculates and returns a discount result;
/// the calling module applies it. Catalog never calculates a discount and never writes to
/// Promotion.
/// </summary>
public class CatalogNeverCalculatesDiscountTests
{
    [Fact]
    public void The_promotion_port_exposes_no_write_operation()
    {
        // The strongest form of "Catalog never writes to Promotion": there is nothing to call.
        var writeVerbs = new[] { "Set", "Create", "Update", "Delete", "Apply", "Save", "Add", "Remove", "Publish" };

        var writeMethods = typeof(IPromotionPricingPort).GetMethods()
            .Where(m => writeVerbs.Any(v => m.Name.StartsWith(v, StringComparison.Ordinal)))
            .Select(m => m.Name)
            .ToList();

        writeMethods.Should().BeEmpty("PRM-001 [not adopted — see architecture-burndown.md BD-005] makes the promotion port strictly read-only");
    }

    [Fact]
    public void Catalog_declares_no_type_that_calculates_a_discount()
    {
        var calculators = ModuleAssemblies.All()
            .Where(a => a.GetName().Name!.StartsWith("ECommerce.Catalog.", StringComparison.Ordinal)
                        && !a.GetName().Name!.EndsWith("Tests", StringComparison.Ordinal))
            .SelectMany(a => a.GetTypes())
            .Where(t => !t.IsNested)
            .Where(t => t.Name.Contains("DiscountCalculat", StringComparison.OrdinalIgnoreCase)
                        || t.Name.Contains("PromotionEngine", StringComparison.OrdinalIgnoreCase)
                        || t.Name.Contains("DiscountRule", StringComparison.OrdinalIgnoreCase))
            .Select(t => t.FullName!)
            .ToList();

        calculators.Should().BeEmpty(
            "FR-011: Catalog displays the discount result Promotion supplies and computes none");
    }

    [Fact]
    public void Catalog_consumes_no_promotion_message_as_a_write_into_promotion()
    {
        // Catalog may reference Promotion.Contracts (ARC-001) but must never reference any
        // other Promotion assembly, which is where a write path would have to live.
        var violations = ModuleAssemblies.All()
            .Where(a => a.GetName().Name!.StartsWith("ECommerce.Catalog.", StringComparison.Ordinal))
            .SelectMany(a => a.GetReferencedAssemblies()
                .Where(r => r.Name!.StartsWith("ECommerce.Promotion.", StringComparison.Ordinal)
                            && r.Name != "ECommerce.Promotion.Contracts")
                .Select(r => $"{a.GetName().Name} -> {r.Name}"))
            .ToList();

        violations.Should().BeEmpty("Catalog reaches Promotion only through its contracts");
    }
}
