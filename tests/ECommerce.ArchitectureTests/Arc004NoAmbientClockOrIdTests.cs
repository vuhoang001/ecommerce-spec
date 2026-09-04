using System.Text.RegularExpressions;
using FluentAssertions;

namespace ECommerce.ArchitectureTests;

/// <summary>
/// ARC-004 — domain code MUST NOT read the system clock or generate identifiers directly;
/// both are injected.
/// </summary>
public class Arc004NoAmbientClockOrIdTests
{
    private static readonly string[] Banned =
        ["DateTime.UtcNow", "DateTime.Now", "DateTimeOffset.UtcNow", "DateTimeOffset.Now",
         "Guid.NewGuid()"];

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "ECommerce.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }

    [Fact]
    public void No_domain_assembly_reads_the_clock_or_mints_an_identifier()
    {
        var domainRoot = Path.Combine(RepoRoot(), "src", "Modules");
        var violations = new List<string>();

        foreach (var file in Directory.EnumerateFiles(domainRoot, "*.cs", SearchOption.AllDirectories))
        {
            // The assembly directory is named e.g. ECommerce.Catalog.Domain, so match on the
            // suffix rather than a bare "Domain" path segment, which never occurs.
            var directory = Path.GetFileName(Path.GetDirectoryName(file)) ?? string.Empty;
            var inDomainAssembly = file.Split(Path.DirectorySeparatorChar)
                .Any(segment => segment.EndsWith(".Domain", StringComparison.Ordinal));
            if (!inDomainAssembly) continue;
            if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")) continue;

            var source = File.ReadAllText(file);
            foreach (var banned in Banned)
            {
                // Ignore occurrences inside comments; the rule is about executed code.
                foreach (var line in source.Split('\n'))
                {
                    var trimmed = line.TrimStart();
                    if (trimmed.StartsWith("//", StringComparison.Ordinal)
                        || trimmed.StartsWith("///", StringComparison.Ordinal)
                        || trimmed.StartsWith("*", StringComparison.Ordinal)) continue;

                    if (line.Contains(banned, StringComparison.Ordinal))
                        violations.Add($"{Path.GetFileName(file)}: {banned}");
                }
            }
        }

        violations.Distinct().Should().BeEmpty(
            "ARC-004 injects time and identity so domain behaviour is testable without waiting " +
            "and reproducible across runs");
    }
}
