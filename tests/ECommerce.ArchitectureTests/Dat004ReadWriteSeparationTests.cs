using System.Reflection;
using FluentAssertions;

namespace ECommerce.ArchitectureTests;

/// <summary>
/// DAT-004 — read paths execute through Dapper; writes execute through the owning module's
/// DbContext. A *Query MUST NOT call SaveChanges; a *Command MUST NOT execute raw SQL.
/// </summary>
public class Dat004ReadWriteSeparationTests
{
    private static readonly string[] WriteMethods =
        ["SaveChanges", "SaveChangesAsync", "ExecuteUpdate", "ExecuteUpdateAsync",
         "ExecuteDelete", "ExecuteDeleteAsync"];

    private static IEnumerable<Type> ProductionTypes() =>
        ModuleAssemblies.All()
            .Where(a => !a.GetName().Name!.EndsWith("Tests", StringComparison.Ordinal))
            .SelectMany(a => a.GetTypes())
            .Where(t => !t.IsNested);

    [Fact]
    public void No_query_type_takes_a_DbContext_dependency()
    {
        // A *Query cannot call SaveChanges if it never receives the write side at all.
        var violations = ProductionTypes()
            .Where(t => t.Name.EndsWith("Query", StringComparison.Ordinal))
            .SelectMany(t => t.GetConstructors()
                .SelectMany(c => c.GetParameters())
                .Where(p => p.ParameterType.Name.EndsWith("DbContext", StringComparison.Ordinal))
                .Select(p => $"{t.Name} takes {p.ParameterType.Name}"))
            .ToList();

        violations.Should().BeEmpty(
            "DAT-004 reserves the DbContext for writes; reads go through Dapper");
    }

    [Fact]
    public void No_query_type_declares_a_write_method_call()
    {
        var violations = ProductionTypes()
            .Where(t => t.Name.EndsWith("Query", StringComparison.Ordinal))
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic |
                                          BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(m => WriteMethods.Contains(m.Name))
                .Select(m => $"{t.Name}.{m.Name}"))
            .ToList();

        violations.Should().BeEmpty("a *Query must not write");
    }

    [Fact]
    public void Every_read_path_depends_on_the_dapper_read_connection()
    {
        var queries = ProductionTypes()
            .Where(t => t.Name.EndsWith("Query", StringComparison.Ordinal) && t.IsClass)
            .ToList();

        queries.Should().NotBeEmpty("this feature has read paths");

        foreach (var query in queries)
        {
            var takesReadConnection = query.GetConstructors()
                .SelectMany(c => c.GetParameters())
                .Any(p => p.ParameterType.Name == "ICatalogReadConnection");

            takesReadConnection.Should().BeTrue(
                "{0} is a read path and DAT-004 routes reads through Dapper", query.Name);
        }
    }
}
