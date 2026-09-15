using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CRM.Infrastructure.Data;

public class MasterErpDbContextFactory : IDesignTimeDbContextFactory<MasterErpDbContext>
{
    public MasterErpDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<MasterErpDbContext>();
        optionsBuilder.UseSqlServer(
            "Server=(localdb)\\MSSQLLocalDB;Database=MSME_MasterERP;Trusted_Connection=True;TrustServerCertificate=True;");

        return new MasterErpDbContext(optionsBuilder.Options);
    }
}