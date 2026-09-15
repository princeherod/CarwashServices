using CRM.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CRM.api.Controllers;

[ApiController]
[Route("api/test-tenant")]
public class TenantTestsController : ControllerBase
{
    private readonly ITenantDbContextFactory _tenantFactory;

    public TenantTestsController(ITenantDbContextFactory tenantFactory)
    {
        _tenantFactory = tenantFactory;
    }

    // GET: api/test-tenant/1
    [HttpGet("{companyId:int}")]
    public async Task<IActionResult> GetTenantInfo(int companyId)
    {
        await using var tenantDb = await _tenantFactory.CreateAsync(companyId);

        var productCount = await tenantDb.Products.CountAsync();

        return Ok(new
        {
            companyId,
            productCount
        });
    }
}