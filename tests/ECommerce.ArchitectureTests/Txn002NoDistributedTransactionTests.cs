using FluentAssertions;

namespace ECommerce.ArchitectureTests;

/// <summary>
/// TXN-002: distributed transactions are FORBIDDEN. TransactionScope and any transaction
/// enlisting more than one resource MUST NOT be used.
/// </summary>
public class Txn002NoDistributedTransactionTests
{
    [Fact]
    public void No_assembly_references_System_Transactions()
    {
        var violations = ModuleAssemblies.All()
            .Where(a => !a.GetName().Name!.EndsWith("Tests", StringComparison.Ordinal))
            .Where(a => a.GetReferencedAssemblies()
                .Any(r => r.Name is "System.Transactions" or "System.Transactions.Local"))
            .Select(a => a.GetName().Name!)
            .ToList();

        violations.Should().BeEmpty("TXN-002 forbids TransactionScope and multi-resource enlistment");
    }
}
