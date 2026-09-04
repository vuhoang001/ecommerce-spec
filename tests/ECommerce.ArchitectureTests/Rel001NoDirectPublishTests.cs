using System.Reflection;
using FluentAssertions;

namespace ECommerce.ArchitectureTests;

/// <summary>
/// REL-001 — a handler MUST NOT publish to the broker. Messages are written to the module's
/// outbox inside the business transaction, and only the relay talks to the bus.
/// </summary>
public class Rel001NoDirectPublishTests
{
    private static readonly string[] BusTypes = ["IBus", "IPublishEndpoint", "ISendEndpointProvider"];

    private static readonly string[] RelayNamespaces = ["ECommerce.Shared.Messaging"];

    [Fact]
    public void No_handler_or_endpoint_depends_on_the_bus_directly()
    {
        var violations = new List<string>();

        foreach (var assembly in ModuleAssemblies.All()
                     .Where(a => !a.GetName().Name!.EndsWith("Tests", StringComparison.Ordinal)))
        {
            foreach (var type in assembly.GetTypes().Where(t => !t.IsNested))
            {
                if (RelayNamespaces.Any(n => type.Namespace?.StartsWith(n, StringComparison.Ordinal) == true))
                    continue;   // the relay is the one place allowed to hold the bus

                var members = type
                    .GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                    .SelectMany(c => c.GetParameters().Select(p => p.ParameterType))
                    .Concat(type.GetFields(BindingFlags.NonPublic | BindingFlags.Instance |
                                           BindingFlags.Public | BindingFlags.DeclaredOnly)
                        .Select(f => f.FieldType));

                foreach (var member in members)
                {
                    if (BusTypes.Contains(member.Name))
                        violations.Add($"{type.FullName} depends on {member.Name}");
                }
            }
        }

        violations.Should().BeEmpty(
            "REL-001 keeps IBus and IPublishEndpoint out of every handler; only the relay publishes");
    }
}
