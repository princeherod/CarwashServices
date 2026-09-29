using System.Threading;
using System.Threading.Tasks;

namespace CRM.Infrastructure.Services;

public interface ITenantDatabaseProvisioner
{
    /// <summary>
    /// Creates the physical database on SQL Server if it doesn't exist,
    /// applies all tenant schema tables (TenantCustomers, Suppliers, Products, Inventories, CustomerInteractions),
    /// and seeds baseline data.
    /// </summary>
    Task<bool> ProvisionDatabaseAsync(
        string serverName,
        string databaseName,
        string? credentialKey = "TenantA",
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Scans all companies in MasterErpDbContext, creates missing CompanyDatabases metadata,
    /// and ensures the physical database for each tenant is created on SQL Server.
    /// </summary>
    Task EnsureAllDatabasesProvisionedAsync(CancellationToken cancellationToken = default);
}
