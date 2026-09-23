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

    // Active list — archived rows are filtered out.
    [HttpGet]
    public async Task<IActionResult> GetAll(int companyId)
    {
        await using var tenantDb = await _tenantFactory.CreateAsync(companyId);
        var customers = await tenantDb.TenantCustomers
            .AsNoTracking()
            .Where(c => !c.IsArchived)
            .OrderBy(c => c.TenantCustomerId)
            .ToListAsync();
        return Ok(customers);
    }

    // Archived list.
    [HttpGet("archived")]
    public async Task<IActionResult> GetArchived(int companyId)
    {
        await using var tenantDb = await _tenantFactory.CreateAsync(companyId);
        var customers = await tenantDb.TenantCustomers
            .AsNoTracking()
            .Where(c => c.IsArchived)
            .OrderByDescending(c => c.ArchivedAt)
            .ToListAsync();
        return Ok(customers);
    }

    [HttpGet("{tenantCustomerId:int}")]
    public async Task<IActionResult> GetById(int companyId, int tenantCustomerId)
    {
        await using var tenantDb = await _tenantFactory.CreateAsync(companyId);
        var customer = await tenantDb.TenantCustomers
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.TenantCustomerId == tenantCustomerId);

        if (customer is null)
            return NotFound(new { message = $"TenantCustomer {tenantCustomerId} not found." });

        return Ok(customer);
    }

    [HttpPost]
    public async Task<IActionResult> Create(int companyId, [FromBody] TenantCustomer customer)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        await using var tenantDb = await _tenantFactory.CreateAsync(companyId);

        var exists = await tenantDb.TenantCustomers.AnyAsync(c => c.CustomerCode == customer.CustomerCode);
        if (exists)
            return Conflict(new { message = $"A tenant customer with code '{customer.CustomerCode}' already exists." });

        customer.IsArchived = false;
        customer.ArchivedAt = null;
        customer.ArchivedBy = null;

        tenantDb.TenantCustomers.Add(customer);
        await tenantDb.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { companyId, tenantCustomerId = customer.TenantCustomerId }, customer);
    }

    [HttpPut("{tenantCustomerId:int}")]
    public async Task<IActionResult> Update(int companyId, int tenantCustomerId, [FromBody] TenantCustomer customer)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        await using var tenantDb = await _tenantFactory.CreateAsync(companyId);
        var existing = await tenantDb.TenantCustomers
            .FirstOrDefaultAsync(c => c.TenantCustomerId == tenantCustomerId);

        if (existing is null)
            return NotFound(new { message = $"TenantCustomer {tenantCustomerId} not found." });

        existing.CustomerCode = customer.CustomerCode;
        existing.CustomerName = customer.CustomerName;
        existing.ContactNumber = customer.ContactNumber;
        existing.EmailAddress = customer.EmailAddress;
        existing.Address = customer.Address;
        existing.IsActive = customer.IsActive;

        existing.PlateNumber = customer.PlateNumber;
        existing.VehicleMake = customer.VehicleMake;
        existing.VehicleModel = customer.VehicleModel;
        existing.VehicleYear = customer.VehicleYear;
        existing.VehicleColor = customer.VehicleColor;
        existing.VehicleType = customer.VehicleType;
        existing.Source = customer.Source;

        // Archive fields are handled by the dedicated endpoints below.
        await tenantDb.SaveChangesAsync();
        return Ok(existing);
    }

    public class ArchiveRequest { public string? ArchivedBy { get; set; } }

    [HttpPut("{tenantCustomerId:int}/archive")]
    public async Task<IActionResult> Archive(int companyId, int tenantCustomerId, [FromBody] ArchiveRequest? req)
    {
        await using var tenantDb = await _tenantFactory.CreateAsync(companyId);
        var existing = await tenantDb.TenantCustomers
            .FirstOrDefaultAsync(c => c.TenantCustomerId == tenantCustomerId);

        if (existing is null)
            return NotFound(new { message = $"TenantCustomer {tenantCustomerId} not found." });
        if (existing.IsArchived)
            return Conflict(new { message = "This customer is already archived." });

        existing.IsArchived = true;
        existing.ArchivedAt = DateTime.UtcNow;
        existing.ArchivedBy = string.IsNullOrWhiteSpace(req?.ArchivedBy) ? "Admin" : req!.ArchivedBy;

        await tenantDb.SaveChangesAsync();
        return Ok(existing);
    }

    [HttpPut("{tenantCustomerId:int}/restore")]
    public async Task<IActionResult> Restore(int companyId, int tenantCustomerId)
    {
        await using var tenantDb = await _tenantFactory.CreateAsync(companyId);
        var existing = await tenantDb.TenantCustomers
            .FirstOrDefaultAsync(c => c.TenantCustomerId == tenantCustomerId);

        if (existing is null)
            return NotFound(new { message = $"TenantCustomer {tenantCustomerId} not found." });
        if (!existing.IsArchived)
            return Conflict(new { message = "This customer is not archived." });

        existing.IsArchived = false;
        existing.ArchivedAt = null;
        existing.ArchivedBy = null;

        await tenantDb.SaveChangesAsync();
        return Ok(existing);
    }

    [HttpDelete("{tenantCustomerId:int}")]
    public async Task<IActionResult> Delete(int companyId, int tenantCustomerId)
    {
        await using var tenantDb = await _tenantFactory.CreateAsync(companyId);
        var customer = await tenantDb.TenantCustomers
            .FirstOrDefaultAsync(c => c.TenantCustomerId == tenantCustomerId);

        if (customer is null)
            return NotFound(new { message = $"TenantCustomer {tenantCustomerId} not found." });

        tenantDb.TenantCustomers.Remove(customer);
        await tenantDb.SaveChangesAsync();
        return NoContent();
    }
}