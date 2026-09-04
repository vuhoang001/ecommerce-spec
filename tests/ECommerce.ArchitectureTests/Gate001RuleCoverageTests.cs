using System.Reflection;
using System.Text.RegularExpressions;
using FluentAssertions;

namespace ECommerce.ArchitectureTests;

/// <summary>
/// GATE-001 / GOV-005 — every rule this plan claims to satisfy is enforced by something that
/// runs. A rule with no test, gate, or review checklist item is a defect in the constitution,
/// so this test makes the claim itself checkable.
/// </summary>
public class Gate001RuleCoverageTests
{
    /// <summary>Rules enforced by something other than a named test, with the artifact that does it.</summary>
    private static readonly Dictionary<string, string> EnforcedElsewhere = new()
    {
        ["DAT-001"] = "CatalogDbContext.HasDefaultSchema + VisibilityFilterTests",
        ["DAT-002"] = "scripts/check-migrations.sh (MigrationGuardTests proves the guard)",
        ["COM-002"] = "docs/reviews/port-review-checklist.md (constitution names review)",
        ["COM-003"] = "docs/reviews/port-review-checklist.md (constitution names review)",
        ["MSG-003"] = "scripts/check-schema-compatibility.sh in CI",
        ["TST-001"] = "commit order: a failing test precedes its implementation",
        ["TST-002"] = "ProductInvariantTests + one test per acceptance criterion",
        ["OBS-001"] = "ProductPriceResolver logging, asserted by the promotion contract tests",
        ["GATE-001"] = "this test",
        ["GATE-004"] = "review process; identifiers are cited throughout plan.md",
        ["STK-001"] = "no component added to the stack; Directory.Packages.props is the record",
        ["STK-004"] = "docs/context-map.md",
        ["REL-002"] = "RelayConcurrencyTests + RelaySqlTests",
        ["REL-003"] = "InboxDeduplicationTests",
        ["REL-004"] = "OutOfOrderDeliveryTests",
        ["REL-005"] = "TolerantReaderTests",
        ["MSG-001"] = "EnvelopeValidationTests",
        ["TXN-001"] = "Txn001OneAggregatePerTransactionTests",
        ["PRM-003"] = "proto oneof makes the absent case unrepresentable; PromotionRejectionTests",
        ["MON-002"] = "N/A — no order exists in this feature",
        ["TXN-003"] = "N/A — no saga in this feature",
        ["TXN-004"] = "N/A — no saga in this feature",
        ["PRM-002"] = "N/A — promotion type logic belongs to the promotion module",
        ["PRM-004"] = "N/A — promotion type logic belongs to the promotion module",
        ["GOV-007"] = "N/A — this feature touches no promotion source file",
        ["STK-002"] = "one Host project and one deployable image; MOD-001 and MOD-005 keep the " +
                      "module boundaries that make extraction a deployment change",
        ["GOV-002"] = "process rule; GOV-005 exempts Governance rules — they govern the review " +
                      "that runs the checks, so nothing mechanical can enforce them",
        ["SPC-001"] = "spec.md keyword scan; the spec passed it and no implementation name appears in it",
    };

    private static readonly Regex RuleId =
        new(@"\b(MOD|DAT|COM|MON|REL|MSG|TXN|PRM|TST|OBS|GATE|STK|GOV|SPC)-\d{3}\b",
            RegexOptions.Compiled);

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "ECommerce.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }

    [Fact]
    public void Every_rule_the_plan_cites_is_enforced_by_a_test_a_gate_or_a_review_artifact()
    {
        var plan = File.ReadAllText(Path.Combine(
            RepoRoot(), "specs", "002-product-catalog", "plan.md"));

        var cited = RuleId.Matches(plan).Select(m => m.Value).Distinct().OrderBy(x => x).ToList();

        // A rule is covered when a test class is named for it, or when it is listed above with
        // the artifact that enforces it.
        var testNames = typeof(Gate001RuleCoverageTests).Assembly.GetTypes()
            .Concat(AppDomain.CurrentDomain.GetAssemblies().SelectMany(SafeTypes))
            .Select(t => t.Name)
            .ToList();

        var uncovered = cited
            .Where(rule => !EnforcedElsewhere.ContainsKey(rule))
            .Where(rule => !testNames.Any(n =>
                n.StartsWith(rule.Replace("-", string.Empty), StringComparison.OrdinalIgnoreCase)))
            .ToList();

        uncovered.Should().BeEmpty(
            "GOV-005: a rule the plan claims to satisfy but nothing enforces is a defect. " +
            "Either add the check or withdraw the claim.");
    }

    [Fact]
    public void The_coverage_map_names_no_rule_the_plan_does_not_cite()
    {
        // Keeps the map honest: an entry for a rule nobody claims is dead weight that would
        // hide a genuine gap behind an exemption.
        var plan = File.ReadAllText(Path.Combine(
            RepoRoot(), "specs", "002-product-catalog", "plan.md"));
        var cited = RuleId.Matches(plan).Select(m => m.Value).ToHashSet();

        EnforcedElsewhere.Keys.Where(k => !cited.Contains(k))
            .Should().BeEmpty("every exemption must correspond to a rule the plan actually cites");
    }

    private static IEnumerable<Type> SafeTypes(Assembly assembly)
    {
        try { return assembly.GetTypes(); }
        catch (ReflectionTypeLoadException ex) { return ex.Types.Where(t => t is not null)!; }
    }
}
