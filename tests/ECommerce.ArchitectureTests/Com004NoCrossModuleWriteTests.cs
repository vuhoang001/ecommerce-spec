using System.Reflection;
using FluentAssertions;

namespace ECommerce.ArchitectureTests;

/// <summary>
/// COM-004 — cross-module writes are events only. No module reaches into another module's
/// Application assembly, which is where a synchronous write would have to enter.
/// </summary>
public class Com004NoCrossModuleWriteTests
{
    [Fact]
    public void No_module_references_another_modules_application_assembly()
    {
        var violations = new List<string>();

        foreach (var assembly in ModuleAssemblies.Modules())
        {
            var name = assembly.GetName().Name!;
            var ownModule = ModuleAssemblies.ModuleOf(name)!;

            foreach (AssemblyName referenced in assembly.GetReferencedAssemblies())
            {
                var refName = referenced.Name!;
                if (!refName.EndsWith(".Application", StringComparison.Ordinal)) continue;
                if (ModuleAssemblies.ModuleOf(refName) is not { } refModule) continue;
                if (refModule == ownModule) continue;

                violations.Add($"{name} -> {refName}");
            }
        }

        violations.Should().BeEmpty(
            "COM-004 makes every cross-module write asynchronous and message-carried");
    }
}
