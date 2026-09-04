using FluentAssertions;

namespace ECommerce.ArchitectureTests;

/// <summary>
/// DAT-005 — every SQL read from a visibility-governed table MUST apply the module's shared
/// visibility fragment. A hand-written visibility clause at a call site is FORBIDDEN.
/// </summary>
/// <remarks>
/// This rule replaces the EF Core global query filter, which Dapper cannot see. Without it a
/// forgotten predicate leaks a Hidden or Discontinued product and nothing errors — the failure
/// is silent, which is why it is scanned for rather than left to review.
/// <para>
/// The check is at file granularity on purpose. The fragment is applied through string
/// interpolation, so the rendered predicate never appears literally in source; asserting on the
/// rendered text would pass for the wrong reasons.
/// </para>
/// </remarks>
public class Dat005VisibilityFragmentTests
{
    private const string GovernedTableRead = "FROM catalog.product";
    private const string SharedFragmentCall = "CatalogVisibility.ActiveOnly";
    private const string HandWrittenClause = "status = 'Active'";

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "ECommerce.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }

    private static IEnumerable<(string Path, string Source)> ProductionSources()
    {
        foreach (var file in Directory.EnumerateFiles(
                     Path.Combine(RepoRoot(), "src"), "*.cs", SearchOption.AllDirectories))
        {
            var sep = Path.DirectorySeparatorChar;
            if (file.Contains($"{sep}obj{sep}") || file.Contains($"{sep}bin{sep}")) continue;
            yield return (file, File.ReadAllText(file));
        }
    }

    [Fact]
    public void Every_file_reading_the_governed_table_applies_the_shared_fragment()
    {
        var violations = ProductionSources()
            .Where(f => !f.Path.EndsWith("CatalogVisibility.cs", StringComparison.Ordinal))
            .Where(f => f.Source.Contains(GovernedTableRead, StringComparison.OrdinalIgnoreCase))
            .Where(f => !f.Source.Contains(SharedFragmentCall, StringComparison.Ordinal))
            .Select(f => Path.GetFileName(f.Path))
            .ToList();

        violations.Should().BeEmpty(
            "DAT-005: a read from catalog.product must compose CatalogVisibility.ActiveOnly");
    }

    [Fact]
    public void No_call_site_writes_its_own_visibility_clause()
    {
        var violations = ProductionSources()
            .Where(f => !f.Path.EndsWith("CatalogVisibility.cs", StringComparison.Ordinal))
            .Where(f => f.Source.Contains(HandWrittenClause, StringComparison.Ordinal))
            .Select(f => Path.GetFileName(f.Path))
            .ToList();

        violations.Should().BeEmpty(
            "DAT-005 forbids a hand-written visibility predicate; only the shared fragment " +
            "renders it, so there is exactly one place to change and one place to get wrong");
    }

    [Fact]
    public void At_least_one_read_path_is_actually_covered_by_this_check()
    {
        // Guards the two assertions above from passing vacuously if the SQL is ever restructured
        // so that no file matches the governed-table pattern at all.
        ProductionSources()
            .Count(f => f.Source.Contains(GovernedTableRead, StringComparison.OrdinalIgnoreCase))
            .Should().BeGreaterThan(0, "the read paths query catalog.product");
    }
}
