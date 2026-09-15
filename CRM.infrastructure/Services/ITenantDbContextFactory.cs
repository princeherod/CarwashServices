using CRM.Infrastructure.Data;

namespace CRM.Infrastructure.Services;

public interface ITenantDbContextFactory
{
    Task<TenantErpDbContext> CreateAsync(int companyId);
}