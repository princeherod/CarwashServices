using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using CRM.Domain.Entities;
using CRM.domain.Entities;
using CRM.Infrastructure.Data;

namespace CRM.Infrastructure.Services;

public class TenantDatabaseProvisioner : ITenantDatabaseProvisioner
{
    private readonly MasterErpDbContext _masterDb;
    private readonly IConfiguration _configuration;
    private readonly ILogger<TenantDatabaseProvisioner> _logger;

    public TenantDatabaseProvisioner(
        MasterErpDbContext masterDb,
        IConfiguration configuration,
        ILogger<TenantDatabaseProvisioner> logger)
    {
        _masterDb = masterDb;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<bool> ProvisionDatabaseAsync(
        string serverName,
        string databaseName,
        string? credentialKey = "TenantA",
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(serverName))
        {
            serverName = "(localdb)\\MSSQLLocalDB";
        }

        if (string.IsNullOrWhiteSpace(databaseName))
        {
            throw new ArgumentException("Database name cannot be null or empty.", nameof(databaseName));
        }

        credentialKey = string.IsNullOrWhiteSpace(credentialKey) ? "TenantA" : credentialKey.Trim();

        var (masterConnStr, tenantConnStr) = BuildConnectionStrings(serverName, databaseName, credentialKey);

        _logger.LogInformation("Checking physical database '{DatabaseName}' on '{ServerName}'...", databaseName, serverName);

        // 1. Physically create database on SQL Server if it does not exist
        using (var masterConn = new SqlConnection(masterConnStr))
        {
            await masterConn.OpenAsync(cancellationToken);
            using var checkCmd = masterConn.CreateCommand();
            checkCmd.CommandText = "SELECT COUNT(1) FROM sys.databases WHERE name = @dbName;";
            checkCmd.Parameters.Add(new SqlParameter("@dbName", databaseName));

            var exists = Convert.ToInt32(await checkCmd.ExecuteScalarAsync(cancellationToken)) > 0;
            if (!exists)
            {
                _logger.LogInformation("Creating physical SQL database '{DatabaseName}'...", databaseName);
                using var createCmd = masterConn.CreateCommand();
                createCmd.CommandText = $"CREATE DATABASE [{databaseName}];";
                await createCmd.ExecuteNonQueryAsync(cancellationToken);
                _logger.LogInformation("Physical database '{DatabaseName}' created successfully.", databaseName);
            }
            else
            {
                _logger.LogInformation("Physical database '{DatabaseName}' already exists on SQL Server.", databaseName);
            }
        }

        // 2. Connect to tenant database and ensure tables and constraints are created
        var options = new DbContextOptionsBuilder<TenantErpDbContext>()
            .UseSqlServer(tenantConnStr)
            .Options;

        using (var tenantDb = new TenantErpDbContext(options))
        {
            var creator = (RelationalDatabaseCreator)tenantDb.Database.GetService<IDatabaseCreator>();
            var hasTables = await creator.HasTablesAsync(cancellationToken);

            if (!hasTables)
            {
                _logger.LogInformation("Creating tables for tenant database '{DatabaseName}'...", databaseName);
                await creator.CreateTablesAsync(cancellationToken);
                _logger.LogInformation("Tables created successfully for '{DatabaseName}'.", databaseName);

                await InitializeMigrationHistoryAsync(tenantConnStr, cancellationToken);
                await SeedBaselineTenantDataAsync(tenantDb, cancellationToken);
            }
            else
            {
                // Ensure any missing tables (e.g. CustomerInteractions) or columns exist on existing databases
                await EnsureTenantTablesAndColumnsAsync(tenantConnStr, cancellationToken);
                await InitializeMigrationHistoryAsync(tenantConnStr, cancellationToken);
            }
        }

        return true;
    }

    public async Task EnsureAllDatabasesProvisionedAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting EnsureAllDatabasesProvisionedAsync check...");

        // 1. Check if any company is missing a CompanyDatabases row
        var companies = await _masterDb.Companies.AsNoTracking().ToListAsync(cancellationToken);
        var existingCompanyDbs = await _masterDb.CompanyDatabases.ToListAsync(cancellationToken);

        bool changesMade = false;
        foreach (var comp in companies)
        {
            var hasDbRecord = existingCompanyDbs.Any(d => d.CompanyId == comp.CompanyId);
            if (!hasDbRecord)
            {
                var defaultDbName = $"Tenant_{comp.CompanyCode}";
                _logger.LogInformation("Company '{CompanyCode}' ({CompanyName}) has no database entry. Adding '{DefaultDbName}'...",
                    comp.CompanyCode, comp.CompanyName, defaultDbName);

                var newDb = new CompanyDatabase
                {
                    CompanyId = comp.CompanyId,
                    ServerName = "(localdb)\\MSSQLLocalDB",
                    DatabaseName = defaultDbName,
                    CredentialKey = "TenantA",
                    IsActive = true
                };
                _masterDb.CompanyDatabases.Add(newDb);
                existingCompanyDbs.Add(newDb);
                changesMade = true;
            }
        }

        if (changesMade)
        {
            await _masterDb.SaveChangesAsync(cancellationToken);
        }

        // 2. Provision physical databases for all registered tenant databases
        var activeDatabases = existingCompanyDbs.Where(d => d.IsActive).ToList();
        _logger.LogInformation("Provisioning {Count} active tenant databases...", activeDatabases.Count);

        foreach (var dbInfo in activeDatabases)
        {
            try
            {
                await ProvisionDatabaseAsync(
                    dbInfo.ServerName,
                    dbInfo.DatabaseName,
                    dbInfo.CredentialKey,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error provisioning tenant database '{DatabaseName}' on '{ServerName}'.",
                    dbInfo.DatabaseName, dbInfo.ServerName);
            }
        }

        _logger.LogInformation("EnsureAllDatabasesProvisionedAsync completed.");
    }

    private (string masterConnStr, string tenantConnStr) BuildConnectionStrings(
        string serverName,
        string databaseName,
        string credentialKey)
    {
        var isLocalDb = serverName.Contains("(localdb)", StringComparison.OrdinalIgnoreCase);

        if (isLocalDb)
        {
            var master = $"Server={serverName};Database=master;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True;";
            var tenant = $"Server={serverName};Database={databaseName};Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True;";
            return (master, tenant);
        }

        var userId = _configuration[$"TenantCredentials:{credentialKey}:UserId"];
        var password = _configuration[$"TenantCredentials:{credentialKey}:Password"];

        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(password))
        {
            // Fall back to trusted connection if credentials not set in appsettings
            var master = $"Server={serverName};Database=master;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True;";
            var tenant = $"Server={serverName};Database={databaseName};Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True;";
            return (master, tenant);
        }
        else
        {
            var master = $"Server={serverName};Database=master;User Id={userId};Password={password};Encrypt=True;TrustServerCertificate=True;MultipleActiveResultSets=True;";
            var tenant = $"Server={serverName};Database={databaseName};User Id={userId};Password={password};Encrypt=True;TrustServerCertificate=True;MultipleActiveResultSets=True;";
            return (master, tenant);
        }
    }

    private async Task InitializeMigrationHistoryAsync(string tenantConnStr, CancellationToken cancellationToken)
    {
        try
        {
            using var conn = new SqlConnection(tenantConnStr);
            await conn.OpenAsync(cancellationToken);

            var createHistorySql = @"
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = '__EFMigrationsHistory')
            BEGIN
                CREATE TABLE [__EFMigrationsHistory] (
                    [MigrationId] nvarchar(150) NOT NULL,
                    [ProductVersion] nvarchar(32) NOT NULL,
                    CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
                );
            END;";

            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = createHistorySql;
                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }

            var migrations = new[]
            {
                "20260914045813_InitialTenantErp",
                "20260914130550_AddTenantCustomersToTenantErp",
                "20260914131805_AddSuppliersToTenantErp",
                "20260914132431_AddInventoryToTenantErp",
                "20260915051031_AddVehicleFieldsToTenantCustomer",
                "20260915151638_AddServiceFieldsToProduct",
                "20260920071218_AddCustomerInteractions",
                "20260923130849_AddArchiveFieldsToTenantEntities",
                "20260929150703_NormalizeTenant3NF"
            };

            foreach (var mig in migrations)
            {
                using var insCmd = conn.CreateCommand();
                insCmd.CommandText = @"
                IF NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId] = @migId)
                BEGIN
                    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
                    VALUES (@migId, '10.0.12');
                END;";
                insCmd.Parameters.Add(new SqlParameter("@migId", mig));
                await insCmd.ExecuteNonQueryAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to update __EFMigrationsHistory on tenant database.");
        }
    }

    private async Task EnsureTenantTablesAndColumnsAsync(string tenantConnStr, CancellationToken cancellationToken)
    {
        try
        {
            using var conn = new SqlConnection(tenantConnStr);
            await conn.OpenAsync(cancellationToken);

            // Check if CustomerInteractions table is missing in existing tenant DB
            var checkInteractionsSql = @"
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'CustomerInteractions')
            BEGIN
                CREATE TABLE [CustomerInteractions] (
                    [InteractionId] int NOT NULL IDENTITY,
                    [CustomerId] int NOT NULL,
                    [Kind] nvarchar(30) NOT NULL,
                    [Severity] nvarchar(20) NULL,
                    [Title] nvarchar(200) NOT NULL,
                    [Details] nvarchar(2000) NULL,
                    [Status] nvarchar(20) NOT NULL,
                    [RecordedBy] nvarchar(200) NULL,
                    [RecordedAt] datetime2 NOT NULL,
                    [ResolvedAt] datetime2 NULL,
                    [ResolutionNotes] nvarchar(max) NULL,
                    CONSTRAINT [PK_CustomerInteractions] PRIMARY KEY ([InteractionId])
                );
                CREATE INDEX [IX_CustomerInteractions_CustomerId] ON [CustomerInteractions] ([CustomerId]);
            END;";

            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = checkInteractionsSql;
                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to verify extra tenant tables.");
        }
    }

    private async Task SeedBaselineTenantDataAsync(TenantErpDbContext tenantDb, CancellationToken cancellationToken)
    {
        try
        {
            if (!await tenantDb.Products.AnyAsync(cancellationToken))
            {
                _logger.LogInformation("Seeding baseline carwash services/products...");
                var products = new[]
                {
                    new Product
                    {
                        ProductCode = "SVC00001",
                        ProductName = "Standard Exterior Wash",
                        Category = "Car Wash",
                        UnitPrice = 150.00m,
                        Description = "High-pressure wash, foam soap cleaning, and spotless hand dry.",
                        IsArchived = false
                    },
                    new Product
                    {
                        ProductCode = "SVC00002",
                        ProductName = "Deluxe Wash & Wax",
                        Category = "Car Wash",
                        UnitPrice = 300.00m,
                        Description = "Foam wash, tire black gloss, hand dry, and premium spray wax protection.",
                        IsArchived = false
                    },
                    new Product
                    {
                        ProductCode = "SVC00003",
                        ProductName = "Interior Vacuum & Wipe Down",
                        Category = "Interior",
                        UnitPrice = 250.00m,
                        Description = "Complete interior vacuuming, floor mat shampoo, and dashboard wipe down.",
                        IsArchived = false
                    },
                    new Product
                    {
                        ProductCode = "SVC00004",
                        ProductName = "Full Detailing & Sanitation",
                        Category = "Detailing",
                        UnitPrice = 1200.00m,
                        Description = "Comprehensive exterior buffing, paint wax, interior deep clean, and engine bay shine.",
                        IsArchived = false
                    },
                    new Product
                    {
                        ProductCode = "SVC00005",
                        ProductName = "Engine Bay Cleaning",
                        Category = "Maintenance",
                        UnitPrice = 400.00m,
                        Description = "Safe degreasing, gentle rinse, and high-temp silicone protective dressing.",
                        IsArchived = false
                    }
                };

                tenantDb.Products.AddRange(products);
                await tenantDb.SaveChangesAsync(cancellationToken);
            }

            if (!await tenantDb.Suppliers.AnyAsync(cancellationToken))
            {
                _logger.LogInformation("Seeding default auto care supplier...");
                tenantDb.Suppliers.Add(new Supplier
                {
                    SupplierCode = "SUP001",
                    SupplierName = "Apex Auto Detailing Supplies Co.",
                    ContactFirstName = "Carlos",
                    ContactLastName = "Mendoza",
                    ContactNumber = "0917-555-0199",
                    EmailAddress = "supplies@apexauto.ph",
                    Address = "45 Industrial Boulevard, Quezon City",
                    IsActive = true
                });
                await tenantDb.SaveChangesAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to seed baseline data for new tenant.");
        }
    }
}
