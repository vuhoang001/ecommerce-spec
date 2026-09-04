using FluentAssertions;

namespace ECommerce.ArchitectureTests;

/// <summary>
/// MOD-005: the system has exactly four modules — catalog, user, order, promotion.
/// Adding or removing one is an amendment under GOV-002.
/// </summary>
public class Mod005ModuleSetTests
{
    [Fact]
    public void No_module_exists_outside_the_four_the_constitution_names()
    {
        var present = ModuleAssemblies.Modules()
            .Select(a => ModuleAssemblies.ModuleOf(a.GetName().Name!)!)
            .Distinct()
            .ToList();

        present.Should().OnlyContain(m => ModuleAssemblies.AllowedModules.Contains(m),
            "MOD-005 fixes the module set at catalog, user, order and promotion");
    }

    [Fact]
    public void Catalog_module_is_present()
    {
        // User and Order are not built by this feature; their absence is expected.
        // This asserts the module this feature owns actually resolves as a module.
        ModuleAssemblies.Modules()
            .Select(a => ModuleAssemblies.ModuleOf(a.GetName().Name!))
            .Should().Contain("Catalog");
    }
}
