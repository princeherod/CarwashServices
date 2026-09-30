using System;
using CRM.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace CRM.Infrastructure.Services;

public class TenantDbContextFactory : ITenantDbContextFactory
{
    private readonly ITenantDatabaseResolver _resolver;
    private readonly IConfiguration _configuration;

    public TenantDbContextFactory(
        ITenantDatabaseResolver resolver,
        IConfiguration configuration)
    {
        _resolver = resolver;
        _configuration = configuration;
    }

    public async Task<TenantErpDbContext> CreateAsync(int companyId)
    {
        var databaseInfo = await _resolver.GetDatabaseInfoAsync(companyId);

        var isLocalDb = databaseInfo.ServerName
            .Contains("(localdb)", StringComparison.OrdinalIgnoreCase);

        string connectionString;

        if (isLocalDb)
        {
            // LocalDB only supports Windows Authentication.
            connectionString =
                $"Server={databaseInfo.ServerName};" +
                $"Database={databaseInfo.DatabaseName};" +
                $"Trusted_Connection=True;" +
                $"TrustServerCertificate=True;" +
                $"Connect Timeout=60;" +
                $"ConnectRetryCount=3;" +
                $"ConnectRetryInterval=10;" +
                $"MultipleActiveResultSets=True;";
        }
        else
        {
            var userId = _configuration[
                $"TenantCredentials:{databaseInfo.CredentialKey}:UserId"];

            var password = _configuration[
                $"TenantCredentials:{databaseInfo.CredentialKey}:Password"];

            if (string.IsNullOrWhiteSpace(userId) ||
                string.IsNullOrWhiteSpace(password))
            {
                throw new InvalidOperationException(
                    $"Credentials not found for key '{databaseInfo.CredentialKey}'.");
            }

            connectionString =
                $"Server={databaseInfo.ServerName};" +
                $"Database={databaseInfo.DatabaseName};" +
                $"User Id={userId};" +
                $"Password={password};" +
                $"Encrypt=True;" +
                $"TrustServerCertificate=True;" +
                $"Connect Timeout=60;" +
                $"ConnectRetryCount=3;" +
                $"ConnectRetryInterval=10;" +
                $"MultipleActiveResultSets=True;";
        }

        var options = new DbContextOptionsBuilder<TenantErpDbContext>()
            .UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure(
                maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(30),
                errorNumbersToAdd: null))
            .Options;

        return new TenantErpDbContext(options);
    }
}