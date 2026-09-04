namespace ECommerce.Catalog.Application.Reads;

/// <summary>
/// DAT-005 — the module's single visibility predicate for raw SQL reads.
/// </summary>
/// <remarks>
/// DAT-004 routes every read through Dapper, and Dapper does not see EF Core global query
/// filters. That removed the mechanism which previously guaranteed FR-001 and SC-002 on every
/// read path — a hidden or discontinued product was unreachable because the filter was applied
/// by the ORM, not by the query author.
/// <para>
/// This fragment replaces it. Every SQL literal selecting from a visibility-governed table MUST
/// compose this constant rather than write its own clause; a hand-written visibility predicate
/// at a call site is FORBIDDEN, because the failure mode is silent — a forgotten clause leaks a
/// concealed product and nothing errors.
/// </para>
/// </remarks>
public static class CatalogVisibility
{
    /// <summary>Tables whose rows are only visible to customers in certain states.</summary>
    public const string GovernedTable = "catalog.product";

    /// <summary>
    /// The predicate. Composed with an explicit alias so a query cannot accidentally apply it to
    /// the wrong table in a join.
    /// </summary>
    public static string ActiveOnly(string alias) => $"{alias}.status = 'Active'";

    /// <summary>
    /// The literal the DAT-005 architecture test scans for. Any SQL selecting from
    /// <see cref="GovernedTable"/> must contain this, which only composing
    /// <see cref="ActiveOnly"/> produces.
    /// </summary>
    public const string RequiredMarker = ".status = 'Active'";
}
