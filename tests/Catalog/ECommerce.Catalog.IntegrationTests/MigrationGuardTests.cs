using System.Diagnostics;
using FluentAssertions;

namespace ECommerce.Catalog.IntegrationTests;

/// <summary>
/// DAT-002 — the CI scanner must reject a foreign key that crosses a schema boundary.
/// This asserts the guard itself works; a guard nobody tests is a guard nobody trusts.
/// </summary>
public class MigrationGuardTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "ECommerce.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }

    private static (int ExitCode, string Output) RunGuard(string target)
    {
        var root = RepoRoot();
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = Path.Combine(root, "scripts", "check-migrations.sh"),
            Arguments = target,
            WorkingDirectory = root,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        })!;
        var output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
        process.WaitForExit();
        return (process.ExitCode, output);
    }

    [Fact]
    public void Accepts_the_real_migrations_which_keep_every_foreign_key_inside_one_schema()
    {
        var (exitCode, output) = RunGuard("src");
        exitCode.Should().Be(0, "no migration in this module crosses a schema boundary");
        output.Should().Contain("DAT-002 OK");
    }

    [Fact]
    public void Rejects_a_migration_declaring_a_foreign_key_into_another_modules_schema()
    {
        var sandbox = Path.Combine(Path.GetTempPath(), "dat002-" + Guid.NewGuid().ToString("N"));
        var migrations = Path.Combine(sandbox, "Modules", "Catalog", "Migrations");
        Directory.CreateDirectory(migrations);
        File.WriteAllText(Path.Combine(migrations, "Offending.cs"),
            """
            table.ForeignKey(
                name: "fk_product_promotion",
                column: x => x.PromotionId,
                principalSchema: "promotion",
                principalTable: "promotion");
            """);

        try
        {
            var (exitCode, output) = RunGuard(sandbox);
            exitCode.Should().NotBe(0, "DAT-002 forbids a foreign key crossing a schema boundary");
            output.Should().Contain("DAT-002 violation");
            output.Should().Contain("promotion");
        }
        finally
        {
            Directory.Delete(sandbox, recursive: true);
        }
    }

    [Fact]
    public void Rejects_a_raw_sql_foreign_key_into_another_modules_schema()
    {
        var sandbox = Path.Combine(Path.GetTempPath(), "dat002-" + Guid.NewGuid().ToString("N"));
        var migrations = Path.Combine(sandbox, "Modules", "Catalog", "Migrations");
        Directory.CreateDirectory(migrations);
        File.WriteAllText(Path.Combine(migrations, "OffendingSql.cs"),
            """
            migrationBuilder.Sql("ALTER TABLE catalog.product ADD CONSTRAINT fk FOREIGN KEY (o) REFERENCES ordering.order (id);");
            """);

        try
        {
            RunGuard(sandbox).ExitCode.Should().NotBe(0, "raw SQL is scanned too, not only the fluent API");
        }
        finally
        {
            Directory.Delete(sandbox, recursive: true);
        }
    }
}
