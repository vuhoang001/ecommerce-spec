using FluentAssertions;

namespace ECommerce.ArchitectureTests;

/// <summary>
/// MOD-003: Shared projects MUST be limited to technical primitives that would still make
/// sense in a banking application. The banned words below all fail that test.
/// </summary>
public class Mod003SharedPrimitivesTests
{
    private static readonly string[] BusinessVocabulary =
        ["Product", "Category", "Cart", "Catalog", "Voucher", "Promotion", "Discount", "Stock", "Order"];

    [Fact]
    public void Shared_assemblies_contain_no_business_types()
    {
        var violations = new List<string>();

        foreach (var assembly in ModuleAssemblies.All()
                     .Where(a => a.GetName().Name!.StartsWith("ECommerce.Shared.", StringComparison.Ordinal)
                                 && !a.GetName().Name!.EndsWith("Tests", StringComparison.Ordinal)))
        {
            foreach (var type in assembly.GetTypes().Where(t => t.IsPublic && !t.IsNested))
            {
                if (BusinessVocabulary.Any(w => type.Name.Contains(w, StringComparison.Ordinal)))
                    violations.Add($"{assembly.GetName().Name}: {type.Name}");
            }
        }

        violations.Should().BeEmpty(
            "MOD-003 asks whether the type would still make sense in a banking application");
    }
}
