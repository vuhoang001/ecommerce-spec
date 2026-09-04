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
        ["REL-006"] = "docs/runbooks/catalog-messaging-replay.md; redelivery and retry are configured explicitly in MessagingSetup. Alerting on the dead-letter queue remains open and is recorded in architecture-burndown.md.",
        ["REL-007"] = "HealthProbeTests, PromotionUnavailableTests",
        ["TXN-001"] = "Txn001OneAggregatePerTransactionTests",
        ["TXN-005"] = "N/A — no order exists in this feature",
        ["QAG-001"] = "commit order: a failing test precedes its implementation",
        ["QAG-002"] = "one test per acceptance criterion",
        ["QAG-003"] = "domain tests reference no infrastructure package",
        ["QAG-004"] = "InboxDeduplicationTests",
        ["QAG-005"] = "N/A — no contended resource is written here",
        ["QAG-006"] = "every infrastructure suite runs on Testcontainers",
        ["SEC-001"] = "N/A — no credential is created, stored or verified here " +
                      "(architecture-burndown.md)",
        ["SEC-002"] = "N/A — no credential storage (architecture-burndown.md)",
        ["SEC-003"] = "N/A for authentication, but the concern is met anyway: FR-002 requires a " +
                      "Hidden product to be reported identically to a non-existent one, asserted " +
                      "byte-for-byte by ProductDetailVisibilityTests",
        ["SEC-004"] = "N/A — the catalogue is anonymous (FR-034) and exposes no per-resource " +
                      "permission (architecture-burndown.md)",
        ["SEC-005"] = "N/A — no security-relevant event on an anonymous read path " +
                      "(architecture-burndown.md)",
        ["SEC-006"] = "PriceRangeValidator, EnvelopeValidator, route constraints",
        ["OBS-001"] = "ProductPriceResolver logging; PromotionRejectionTests",
        ["SPC-001"] = "scripts/check-spec-keywords.sh",
        ["STK-001"] = "scripts/check-approved-packages.sh",
        ["GATE-001"] = "this test",
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
        // Only rules the document actually DEFINES, i.e. "- **XXX-000**: ...".
        // A WITHDRAWN rule keeps its identifier in place by design, but it is no longer a rule
        // to assess — counting it would make the unassessed tally permanently wrong.
        return Regex.Matches(text, @"^\- \*\*([A-Z]{3,4}-\d{3})\*\*(.*)$", RegexOptions.Multiline)
            .Where(m => !m.Groups[2].Value.Contains("WITHDRAWN", StringComparison.Ordinal))
            .Select(m => m.Groups[1].Value)
            .ToHashSet();
    }

    private static HashSet<string> PlanCitations()
    {
        var plan = Read("specs", "002-product-catalog", "plan.md");
        return RuleId.Matches(plan)
            .Select(m => m.Value)
            // Citations explicitly marked as retired are documentation, not compliance claims.
            // A row reading "| TXN-004 | WITHDRAWN |" records that the withdrawal was noticed;
            // it does not assert conformance with a rule that no longer binds anything.
            .Where(id => !plan.Contains($"{id} [not adopted", StringComparison.Ordinal)
                         && !plan.Contains($"{id} [not a rule", StringComparison.Ordinal)
                         && !plan.Contains($"{id} [withdrawn citation]", StringComparison.Ordinal)
                         && !Regex.IsMatch(plan, $@"\|\s*{Regex.Escape(id)}\s*\|\s*WITHDRAWN"))
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

    /// <summary>
    /// Test class names, read from source on disk rather than from loaded assemblies.
    /// </summary>
    /// <remarks>
    /// A previous version enumerated <c>AppDomain.CurrentDomain.GetAssemblies()</c>, which returns
    /// only what has already been loaded. Whether a given test assembly was loaded depended on
    /// which xUnit test happened to run first, so the check was a race — it passed locally and
    /// failed in CI on the same commit. Reading declarations from source is deterministic and
    /// independent of load order and of which project is executing.
    /// </remarks>
    private static HashSet<string> TestClassNames()
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        var testRoot = Path.Combine(RepoRoot(), "tests");

        foreach (var file in Directory.EnumerateFiles(testRoot, "*.cs", SearchOption.AllDirectories))
        {
            var sep = Path.DirectorySeparatorChar;
            if (file.Contains($"{sep}obj{sep}") || file.Contains($"{sep}bin{sep}")) continue;

            foreach (Match declaration in Regex.Matches(
                         File.ReadAllText(file), @"\bclass\s+([A-Za-z_][A-Za-z0-9_]*)"))
            {
                names.Add(declaration.Groups[1].Value);
            }
        }

        // A silently empty scan is how this check came to pass while doing nothing: the pattern
        // held a stray control character, matched no declaration, and every caller compared
        // against an empty set. Fail loudly instead.
        if (names.Count == 0)
            throw new InvalidOperationException(
                $"No class declarations found under '{testRoot}'. The scan is broken, and every " +
                "check built on it would pass vacuously.");

        return names;
    }

    [Fact]
    public void Every_cited_rule_is_enforced_by_a_named_test_a_gate_or_a_recorded_deviation()
    {
        var testNames = TestClassNames();

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

    [Fact]
    public void Every_script_the_map_names_exists_and_runs_in_ci()
    {
        // The map was free text that nothing verified. That is how SPC-001 came to claim a scan
        // nobody had written: the justification read plausibly and no test could tell. A named
        // script that does not exist, or exists but never runs, is not an enforcement mechanism.
        var workflow = Read(".github", "workflows", "ci.yml");
        var problems = new List<string>();

        foreach (var (rule, justification) in EnforcedWithoutATest)
        {
            foreach (Match reference in Regex.Matches(justification, @"scripts/[A-Za-z0-9._-]+\.sh"))
            {
                var script = reference.Value;

                if (!File.Exists(Path.Combine(RepoRoot(), script)))
                    problems.Add($"{rule} names {script}, which does not exist");
                else if (!workflow.Contains(script, StringComparison.Ordinal))
                    problems.Add($"{rule} names {script}, which is not a step in ci.yml");
            }
        }

        problems.Should().BeEmpty("a rule enforced by a script that does not run is unenforced");
    }

    [Fact]
    public void Every_document_the_map_names_exists()
    {
        var problems = new List<string>();

        foreach (var (rule, justification) in EnforcedWithoutATest)
        {
            foreach (Match reference in Regex.Matches(justification, @"docs/[A-Za-z0-9._/-]+\.md"))
            {
                if (!File.Exists(Path.Combine(RepoRoot(), reference.Value)))
                    problems.Add($"{rule} names {reference.Value}, which does not exist");
            }
        }

        problems.Should().BeEmpty("a rule enforced by a document nobody wrote is unenforced");
    }

    [Fact]
    public void Every_test_class_the_map_names_actually_exists()
    {
        var declared = TestClassNames();
        var problems = new List<string>();

        foreach (var (rule, justification) in EnforcedWithoutATest)
        {
            foreach (Match reference in Regex.Matches(justification, @"\b([A-Z][A-Za-z0-9]*Tests)\b"))
            {
                if (!declared.Contains(reference.Groups[1].Value))
                    problems.Add($"{rule} names {reference.Groups[1].Value}, which is not a test class");
            }
        }

        problems.Should().BeEmpty(
            "a renamed or deleted test leaves the map asserting a check that is gone. " +
            $"Scanned {declared.Count} declared class name(s) under " +
            $"'{Path.Combine(RepoRoot(), "tests")}' " +
            $"(exists: {Directory.Exists(Path.Combine(RepoRoot(), "tests"))}, " +
            $"files: {(Directory.Exists(Path.Combine(RepoRoot(), "tests")) ? Directory.EnumerateFiles(Path.Combine(RepoRoot(), "tests"), "*.cs", SearchOption.AllDirectories).Count() : -1)}); " +
            $"a sample: {string.Join(", ", declared.OrderBy(n => n).Take(6))}");
    }

    [Fact]
    public void Every_burndown_reference_the_map_makes_is_still_open()
    {
        // A justification pointing at a CLOSED deviation is stale in the most misleading way: it
        // reads as "tracked elsewhere" while the elsewhere has already been resolved, so nobody
        // looks again. REL-006 cited BD-003 for exactly as long as BD-003 was closed.
        var burndown = Read("architecture-burndown.md");
        var openSection = burndown[..(burndown.IndexOf("## Closed", StringComparison.Ordinal) is var i && i > 0
            ? i
            : burndown.Length)];

        var problems = new List<string>();

        foreach (var (rule, justification) in EnforcedWithoutATest)
        {
            foreach (Match reference in Regex.Matches(justification, @"\bBD-\d{3}\b"))
            {
                if (!openSection.Contains(reference.Value, StringComparison.Ordinal))
                    problems.Add($"{rule} cites {reference.Value}, which is not an open deviation");
            }
        }

        problems.Should().BeEmpty("citing a closed deviation hides a gap behind a resolved reference");
    }
}
