using System;
using System.Collections.Generic;
using System.Text;

namespace CRM.Infrastructure.Services;

public interface ITenantDatabaseResolver
{
    Task<TenantDatabaseInfo> GetDatabaseInfoAsync(int companyId);
}
