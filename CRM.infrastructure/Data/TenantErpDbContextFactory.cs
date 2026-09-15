using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CRM.Infrastructure.Data;

public class TenantErpDbContextFactory
    : IDesignTimeDbContextFactory<TenantErpDbContext>
{
    public TenantErpDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<TenantErpDbContext>();
        optionsBuilder.UseSqlServer(
             "Server=(localdb)\\MSSQLLocalDB;Database=TenantErpDb;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True;");

        return new TenantErpDbContext(optionsBuilder.Options);
    }
}