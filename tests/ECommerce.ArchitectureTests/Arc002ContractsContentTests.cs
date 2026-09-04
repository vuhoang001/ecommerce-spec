using FluentAssertions;

namespace ECommerce.ArchitectureTests;

/// <summary>
/// ARC-002: A .Contracts assembly MUST contain only event schemas, generated proto types
/// and port interfaces — no entities, no EF Core types, no handlers.
/// </summary>
public class Arc002ContractsContentTests
{
    private static readonly string[] ForbiddenSuffixes =
        ["DbContext", "Handler", "Consumer", "Repository", "Configuration", "Migration"];

    [Fact]
    public void Contracts_assemblies_declare_no_entity_ef_type_or_handler()
    {
        var violations = new List<string>();

        foreach (var assembly in ModuleAssemblies.All()
                     .Where(a => ModuleAssemblies.IsContracts(a.GetName().Name!)))
        {
            foreach (var type in assembly.GetTypes().Where(t => !t.IsNested))
            {
                if (ForbiddenSuffixes.Any(s => type.Name.EndsWith(s, StringComparison.Ordinal)))
                    violations.Add($"{assembly.GetName().Name}: {type.Name}");

                if (type.BaseType?.Name == "DbContext")
                    violations.Add($"{assembly.GetName().Name}: {type.Name} derives from DbContext");
            }
        }

        violations.Should().BeEmpty("ARC-002 keeps .Contracts free of entities, EF types and handlers");
    }
}
