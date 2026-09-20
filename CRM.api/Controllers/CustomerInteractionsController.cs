using CRM.Domain.Entities;
using CRM.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CRM.api.Controllers;

[ApiController]
[Route("api/tenant/{companyId:int}/customer-interactions")]
public class CustomerInteractionsController : ControllerBase
{
    private readonly ITenantDbContextFactory _tenantFactory;

    public CustomerInteractionsController(ITenantDbContextFactory tenantFactory)
    {
        _tenantFactory = tenantFactory;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(int companyId, [FromQuery] int? customerId = null)
    {
        await using var db = await _tenantFactory.CreateAsync(companyId);
        var q = db.CustomerInteractions.AsNoTracking().AsQueryable();
        if (customerId.HasValue) q = q.Where(x => x.CustomerId == customerId.Value);
        var rows = await q.OrderByDescending(x => x.CreatedAt).ToListAsync();
        return Ok(rows);
    }

    [HttpPost]
    public async Task<IActionResult> Create(int companyId, [FromBody] CustomerInteraction req)
    {
        await using var db = await _tenantFactory.CreateAsync(companyId);
        req.CreatedAt = DateTime.UtcNow;
        if (string.IsNullOrWhiteSpace(req.Status)) req.Status = "Open";
        db.CustomerInteractions.Add(req);
        await db.SaveChangesAsync();
        return Ok(req);
    }
}