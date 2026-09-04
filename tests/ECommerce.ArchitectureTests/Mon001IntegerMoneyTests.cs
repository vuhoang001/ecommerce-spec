using System.Reflection;
using FluentAssertions;

namespace ECommerce.ArchitectureTests;

/// <summary>
/// MON-001: every monetary value is an integer in the smallest currency unit.
/// Floating-point types MUST NOT appear in a monetary calculation.
/// </summary>
public class Mon001IntegerMoneyTests
{
    private static readonly Type[] Banned = [typeof(float), typeof(double), typeof(decimal)];

    private static readonly string[] MoneyWords =
        ["Price", "Amount", "Total", "Discount", "Money", "Cost", "Fee", "Balance"];

    [Fact]
    public void No_money_member_uses_a_floating_point_or_decimal_type()
    {
        var violations = new List<string>();

        foreach (var assembly in ModuleAssemblies.All()
                     .Where(a => !a.GetName().Name!.EndsWith("Tests", StringComparison.Ordinal)))
        {
            foreach (var type in assembly.GetTypes().Where(t => !t.IsNested))
            {
                foreach (var p in type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic |
                                                     BindingFlags.Instance | BindingFlags.Static |
                                                     BindingFlags.DeclaredOnly))
                {
                    if (IsMoneyName(p.Name) && Banned.Contains(Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType))
                        violations.Add($"{type.FullName}.{p.Name} : {p.PropertyType.Name}");
                }

                foreach (var f in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic |
                                                 BindingFlags.Instance | BindingFlags.Static |
                                                 BindingFlags.DeclaredOnly))
                {
                    if (f.Name.Contains('<')) continue; // compiler-generated backing field
                    if (IsMoneyName(f.Name) && Banned.Contains(Nullable.GetUnderlyingType(f.FieldType) ?? f.FieldType))
                        violations.Add($"{type.FullName}.{f.Name} : {f.FieldType.Name}");
                }
            }
        }

        violations.Should().BeEmpty("MON-001 bans float, double and decimal from every money path");
    }

    private static bool IsMoneyName(string name) =>
        MoneyWords.Any(w => name.Contains(w, StringComparison.OrdinalIgnoreCase));
}
