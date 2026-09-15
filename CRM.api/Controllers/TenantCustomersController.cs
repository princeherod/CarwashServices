using CRM.Domain.Entities;
using CRM.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CRM.api.Controllers;

[ApiController]
[Route("api/tenant/{companyId:int}/tenant-customers")]
public class TenantCustomersController : ControllerBase
{
    private readonly ITenantDbContextFactory _tenantFactory;

    public TenantCustomersController(ITenantDbContextFactory tenantFactory)
    {
        _tenantFactory = tenantFactory;
    }

    // GET: api/tenant/1/tenant-customers
    [HttpGet]
    public async Task<IActionResult> GetAll(int companyId)
    {
        await using var tenantDb = await _tenantFactory.CreateAsync(companyId);

        var customers = await tenantDb.TenantCustomers
            .AsNoTracking()
            .OrderBy(c => c.TenantCustomerId)
            .ToListAsync();

        return Ok(customers);
    }

    // GET: api/tenant/1/tenant-customers/5
    [HttpGet("{tenantCustomerId:int}")]
    public async Task<IActionResult> GetById(int companyId, int tenantCustomerId)
    {
        await using var tenantDb = await _tenantFactory.CreateAsync(companyId);

        var customer = await tenantDb.TenantCustomers
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.TenantCustomerId == tenantCustomerId);

        if (customer is null)
        {
            return NotFound(new
            {
                message = $"TenantCustomer {tenantCustomerId} not found in tenant {companyId}."
            });
        }

        return Ok(customer);
    }

    // POST: api/tenant/1/tenant-customers
    [HttpPost]
    public async Task<IActionResult> Create(int companyId, [FromBody] TenantCustomer customer)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        await using var tenantDb = await _tenantFactory.CreateAsync(companyId);

        var exists = await tenantDb.TenantCustomers
            .AnyAsync(c => c.CustomerCode == customer.CustomerCode);

        if (exists)
        {
            return Conflict(new
            {
                message = $"A tenant customer with code '{customer.CustomerCode}' already exists in tenant {companyId}."
            });
        }

        tenantDb.TenantCustomers.Add(customer);
        await tenantDb.SaveChangesAsync();

        return CreatedAtAction(
            nameof(GetById),
            new { companyId, tenantCustomerId = customer.TenantCustomerId },
            customer);
    }

    // DELETE: api/tenant/1/tenant-customers/5
    [HttpDelete("{tenantCustomerId:int}")]
    public async Task<IActionResult> Delete(int companyId, int tenantCustomerId)
    {
        await using var tenantDb = await _tenantFactory.CreateAsync(companyId);

        var customer = await tenantDb.TenantCustomers
            .FirstOrDefaultAsync(c => c.TenantCustomerId == tenantCustomerId);

        if (customer is null)
        {
            return NotFound(new
            {
                message = $"TenantCustomer {tenantCustomerId} not found in tenant {companyId}."
            });
        }

        tenantDb.TenantCustomers.Remove(customer);
        await tenantDb.SaveChangesAsync();

        return NoContent();
    }
}