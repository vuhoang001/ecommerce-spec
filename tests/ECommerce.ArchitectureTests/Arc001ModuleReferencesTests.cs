using System.Reflection;
using FluentAssertions;

namespace ECommerce.ArchitectureTests;

/// <summary>
/// ARC-001: A module MUST NOT reference any assembly of another module except that
/// module's .Contracts assembly.
/// </summary>
public class Arc001ModuleReferencesTests
{
    [Fact]
    public void Module_references_only_another_modules_contracts()
    {
        var violations = new List<string>();

        foreach (var assembly in ModuleAssemblies.Modules())
        {
            var name = assembly.GetName().Name!;
            var ownModule = ModuleAssemblies.ModuleOf(name)!;

            foreach (AssemblyName referenced in assembly.GetReferencedAssemblies())
            {
                var refName = referenced.Name!;
                var refModule = ModuleAssemblies.ModuleOf(refName);
                if (refModule is null || refModule == ownModule) continue;
                if (ModuleAssemblies.IsContracts(refName)) continue;

                violations.Add($"{name} -> {refName}");
            }
        }

        violations.Should().BeEmpty(
            "ARC-001 allows a cross-module reference only to that module's .Contracts assembly");
    }
}
