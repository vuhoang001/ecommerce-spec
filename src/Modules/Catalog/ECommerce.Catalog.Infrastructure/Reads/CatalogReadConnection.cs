using System.Data;
using ECommerce.Catalog.Application.Reads;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ECommerce.Catalog.Infrastructure.Reads;

/// <summary>
/// Opens a connection to this module's own database for Dapper reads (DAT-004).
/// </summary>
/// <remarks>
/// It takes the connection string from the module's DbContext so read and write paths can never
/// drift onto different databases, while remaining a distinct connection — a read must not
/// enlist in a write's transaction.
/// </remarks>
public sealed class CatalogReadConnection(CatalogDbContext db) : ICatalogReadConnection
{
    public async Task<IDbConnection> OpenAsync(CancellationToken ct = default)
    {
        var connection = new NpgsqlConnection(db.Database.GetConnectionString());
        await connection.OpenAsync(ct);
        return connection;
    }
}
