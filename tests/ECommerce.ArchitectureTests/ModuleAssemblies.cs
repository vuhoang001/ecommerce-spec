using System.Reflection;

namespace ECommerce.ArchitectureTests;

/// <summary>Shared assembly discovery for the constitution's architecture gates.</summary>
internal static class ModuleAssemblies
{
    internal static readonly string[] AllowedModules = ["Catalog", "User", "Order", "Promotion"];

    internal static IReadOnlyList<Assembly> All()
    {
        var dir = Path.GetDirectoryName(typeof(ModuleAssemblies).Assembly.Location)!;
        var loaded = new List<Assembly>();
        foreach (var dll in Directory.GetFiles(dir, "ECommerce.*.dll"))
        {
            try { loaded.Add(Assembly.LoadFrom(dll)); } catch { /* not managed */ }
        }
        return loaded;
    }

    internal static IReadOnlyList<Assembly> Modules() =>
        All().Where(a => ModuleOf(a.GetName().Name!) is not null).ToList();

    /// <summary>ECommerce.Catalog.Domain -> "Catalog". Null for shared, host and test assemblies.</summary>
    internal static string? ModuleOf(string assemblyName)
    {
        var parts = assemblyName.Split('.');
        if (parts.Length < 3 || parts[0] != "ECommerce") return null;
        if (parts[1] is "Shared" or "Host") return null;
        if (assemblyName.EndsWith("Tests", StringComparison.Ordinal)) return null;
        return parts[1];
    }

    internal static bool IsContracts(string assemblyName) =>
        assemblyName.EndsWith(".Contracts", StringComparison.Ordinal);
}
