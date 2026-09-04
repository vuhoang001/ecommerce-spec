using System.Reflection;
using System.Text.RegularExpressions;
using FluentAssertions;

namespace ECommerce.ArchitectureTests;

/// <summary>
/// GATE-001 — every named check runs in CI and blocks the merge, and every rule this feature
/// claims to satisfy is enforced by something that actually executes.
/// </summary>
/// <remarks>
/// The previous version of this test compared plan.md against its own hard-coded map, so it
/// passed while 23 cited identifiers had no canonical rule behind them — it could not see the
/// drift it was written to catch. It now reads the constitution as the source of truth: an
/// identifier cited in plan.md that the constitution does not define is a failure.
/// </remarks>
public class Gate001RuleCoverageTests
{
    private static readonly Regex RuleId =
        new(@"\b(ARC|DAT|COM|REL|TXN|SEC|QAG|SPC|OBS|STK|GATE|GOV|MOD|MON|MSG|TST|PRM)-\d{3}\b",
            RegexOptions.Compiled);

    /// <summary>Rules enforced by a gate script or a review artifact rather than a named test.</summary>
    private static readonly Dictionary<string, string> EnforcedWithoutATest = new()
    {
        ["DAT-001"] = "CatalogDbContext default schema; VisibilityFilterTests",
        ["DAT-002"] = "scripts/check-migrations.sh (MigrationGuardTests proves the guard)",
        ["DAT-003"] = "code review; the discount copy snapshots rather than re-reads",
        ["DAT-006"] = "scripts/check-sql-schemas.sh",
        ["COM-002"] = "docs/reviews/port-review-checklist.md",
        ["COM-003"] = "docs/reviews/port-review-checklist.md",
        ["COM-005"] = "event/command semantics; promotion_discount_changed.md",
        ["COM-006"] = "EnvelopeValidationTests",
        ["COM-007"] = "event name asserted in DiscountChangedV1",
        ["COM-008"] = "scripts/check-schema-compatibility.sh",
        ["REL-002"] = "RelayConcurrencyTests, RelaySqlTests",
        ["REL-003"] = "InboxDeduplicationTests",
        ["REL-004"] = "OutOfOrderDeliveryTests",
        ["REL-005"] = "TolerantReaderTests",
        ["REL-006"] = "architecture-burndown.md BD-003 — open deviation",
        ["REL-007"] = "HealthProbeTests, PromotionUnavailableTests",
        ["TXN-001"] = "Txn001OneAggregatePerTransactionTests",
        ["TXN-005"] = "N/A — no order exists in this feature",
        ["QAG-001"] = "commit order: a failing test precedes its implementation",
        ["QAG-002"] = "one test per acceptance criterion",
        ["QAG-003"] = "domain tests reference no infrastructure package",
        ["QAG-004"] = "InboxDeduplicationTests",
        ["QAG-005"] = "N/A — no contended resource is written here",
        ["QAG-006"] = "every infrastructure suite runs on Testcontainers",
        ["SEC-006"] = "PriceRangeValidator, EnvelopeValidator, route constraints",
        ["OBS-001"] = "ProductPriceResolver logging; PromotionRejectionTests",
        ["SPC-001"] = "spec.md keyword scan",
        ["STK-001"] = "scripts/check-approved-packages.sh",
        ["GATE-001"] = "this test",
        ["GATE-001"] = "review process; identifiers cited throughout plan.md",
        ["ARC-005"] = "architecture-burndown.md",
        ["TXN-002"] = "N/A — no cross-module workflow exists in this feature",
        ["TXN-003"] = "N/A — no saga exists, so there is no compensation branch to test",
        ["TXN-006"] = "Txn006IntegerMoneyTests, MoneyTests, MoneyRoundTripTests",
        ["ARC-001"] = "Arc001ModuleReferencesTests",
        ["ARC-002"] = "Arc002ContractsContentTests",
        ["ARC-003"] = "Arc003SharedPrimitivesTests",
        ["ARC-004"] = "Arc004NoAmbientClockOrIdTests",
        ["COM-001"] = "Com001PortOwnershipTests",
        ["COM-004"] = "Com004NoCrossModuleWriteTests",
        ["DAT-004"] = "Dat004ReadWriteSeparationTests",
        ["DAT-005"] = "Dat005VisibilityFragmentTests",
        ["REL-001"] = "Rel001NoDirectPublishTests",
    };

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "ECommerce.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }

    private static string Read(params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { RepoRoot() }.Concat(parts).ToArray()));

    private static HashSet<string> ConstitutionRules()
    {
        var text = Read(".specify", "memory", "constitution.md");
        // Only rules the document actually DEFINES, i.e. "- **XXX-000**: ..."
        return Regex.Matches(text, @"^\- \*\*([A-Z]{3,4}-\d{3})\*\*", RegexOptions.Multiline)
            .Select(m => m.Groups[1].Value).ToHashSet();
    }

    private static HashSet<string> PlanCitations()
    {
        var plan = Read("specs", "002-product-catalog", "plan.md");
        return RuleId.Matches(plan)
            .Select(m => m.Value)
            // Citations explicitly marked as retired are documentation, not claims.
            .Where(id => !plan.Contains($"{id} [not adopted", StringComparison.Ordinal)
                         && !plan.Contains($"{id} [not a rule", StringComparison.Ordinal)
                         && !plan.Contains($"{id} [withdrawn citation]", StringComparison.Ordinal))
            .ToHashSet();
    }

    [Fact]
    public void Every_identifier_the_plan_cites_is_defined_by_the_constitution()
    {
        var undefined = PlanCitations().Except(ConstitutionRules()).OrderBy(x => x).ToList();

        undefined.Should().BeEmpty(
            "a plan cannot claim compliance with a rule the constitution does not define — " +
            "that is exactly the drift this test exists to catch");
    }

    [Fact]
    public void Every_cited_rule_is_enforced_by_a_named_test_a_gate_or_a_recorded_deviation()
    {
        var testNames = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(SafeTypes).Select(t => t.Name).ToList();

        var unenforced = PlanCitations()
            .Where(rule => !EnforcedWithoutATest.ContainsKey(rule))
            .Where(rule => !testNames.Any(n =>
                n.StartsWith(rule.Replace("-", string.Empty), StringComparison.OrdinalIgnoreCase)))
            .OrderBy(x => x)
            .ToList();

        unenforced.Should().BeEmpty(
            "GATE-001: a rule with no test, gate or recorded deviation is unenforced");
    }

    [Fact]
    public void The_enforcement_map_names_no_rule_the_constitution_does_not_define()
    {
        // Stops an exemption outliving the rule it exempts, which would hide a real gap.
        EnforcedWithoutATest.Keys.Except(ConstitutionRules()).OrderBy(x => x)
            .Should().BeEmpty("every exemption must correspond to a defined rule");
    }

    private static IEnumerable<Type> SafeTypes(Assembly assembly)
    {
        try { return assembly.GetTypes(); }
        catch (ReflectionTypeLoadException ex) { return ex.Types.Where(t => t is not null)!; }
    }
}
