using System.Data;

namespace ECommerce.Catalog.Application.Reads;

/// <summary>
/// DAT-004 — the read side's connection. Separate from the module's DbContext, which DAT-004
/// reserves for writes.
/// </summary>
public interface ICatalogReadConnection
{
    Task<IDbConnection> OpenAsync(CancellationToken ct = default);
}
