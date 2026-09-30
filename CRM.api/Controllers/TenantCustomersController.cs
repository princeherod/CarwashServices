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
    public async Task<IActionResult> GetAll(int companyId, [FromQuery] int? branchId = null)
    {
        await using var tenantDb = await _tenantFactory.CreateAsync(companyId);
        var query = tenantDb.TenantCustomers
            .AsNoTracking()
            .Where(c => !c.IsArchived);

        if (branchId.HasValue && branchId.Value > 0)
        {
            query = query.Where(c => c.BranchId == branchId.Value);
        }

        var customers = await query
            .OrderBy(c => c.TenantCustomerId)
            .ToListAsync();
        return Ok(customers);
    }

    // Archived list.
    [HttpGet("archived")]
    public async Task<IActionResult> GetArchived(int companyId, [FromQuery] int? branchId = null)
    {
        await using var tenantDb = await _tenantFactory.CreateAsync(companyId);
        var query = tenantDb.TenantCustomers
            .AsNoTracking()
            .Where(c => c.IsArchived);

        if (branchId.HasValue && branchId.Value > 0)
        {
            query = query.Where(c => c.BranchId == branchId.Value);
        }

        var customers = await query
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

        if (string.IsNullOrWhiteSpace(customer.FirstName) || string.IsNullOrWhiteSpace(customer.LastName))
        {
            if (!string.IsNullOrWhiteSpace(customer.CustomerName))
            {
                var full = customer.CustomerName.Trim();
                var idx = full.IndexOf(' ');
                if (idx > 0)
                {
                    if (string.IsNullOrWhiteSpace(customer.FirstName)) customer.FirstName = full.Substring(0, idx).Trim();
                    if (string.IsNullOrWhiteSpace(customer.LastName)) customer.LastName = full.Substring(idx + 1).Trim();
                }
                else
                {
                    if (string.IsNullOrWhiteSpace(customer.FirstName)) customer.FirstName = full;
                    if (string.IsNullOrWhiteSpace(customer.LastName)) customer.LastName = "-";
                }
            }
        }

        if (string.IsNullOrWhiteSpace(customer.Street) && string.IsNullOrWhiteSpace(customer.City) && !string.IsNullOrWhiteSpace(customer.Address))
        {
            var parts = customer.Address.Split(',');
            if (parts.Length >= 3)
            {
                customer.Street = parts[0].Trim();
                customer.City = parts[1].Trim();
                customer.Province = string.Join(", ", parts.Skip(2)).Trim();
            }
            else if (parts.Length == 2)
            {
                customer.Street = parts[0].Trim();
                customer.City = parts[1].Trim();
            }
            else
            {
                customer.Street = customer.Address.Trim();
            }
        }

        await using var tenantDb = await _tenantFactory.CreateAsync(companyId);

        var exists = await tenantDb.TenantCustomers.AnyAsync(c => c.CustomerCode == customer.CustomerCode);
        if (exists)
            return Conflict(new { message = $"A tenant customer with code '{customer.CustomerCode}' already exists." });

        customer.IsArchived = false;
        customer.ArchivedAt = null;
        customer.ArchivedBy = null;

        if (!customer.BranchId.HasValue || customer.BranchId.Value <= 0)
        {
            var defBranch = await tenantDb.Branches.FirstOrDefaultAsync(b => b.IsMainBranch && !b.IsArchived)
                ?? await tenantDb.Branches.FirstOrDefaultAsync(b => !b.IsArchived);
            if (defBranch != null)
            {
                customer.BranchId = defBranch.BranchId;
            }
        }

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

        if (customer.BranchId.HasValue && customer.BranchId.Value > 0)
        {
            existing.BranchId = customer.BranchId.Value;
        }

        if (string.IsNullOrWhiteSpace(customer.FirstName) || string.IsNullOrWhiteSpace(customer.LastName))
        {
            if (!string.IsNullOrWhiteSpace(customer.CustomerName))
            {
                var full = customer.CustomerName.Trim();
                var idx = full.IndexOf(' ');
                if (idx > 0)
                {
                    if (string.IsNullOrWhiteSpace(customer.FirstName)) customer.FirstName = full.Substring(0, idx).Trim();
                    if (string.IsNullOrWhiteSpace(customer.LastName)) customer.LastName = full.Substring(idx + 1).Trim();
                }
                else
                {
                    if (string.IsNullOrWhiteSpace(customer.FirstName)) customer.FirstName = full;
                    if (string.IsNullOrWhiteSpace(customer.LastName)) customer.LastName = "-";
                }
            }
        }

        if (string.IsNullOrWhiteSpace(customer.Street) && string.IsNullOrWhiteSpace(customer.City) && !string.IsNullOrWhiteSpace(customer.Address))
        {
            var parts = customer.Address.Split(',');
            if (parts.Length >= 3)
            {
                customer.Street = parts[0].Trim();
                customer.City = parts[1].Trim();
                customer.Province = string.Join(", ", parts.Skip(2)).Trim();
            }
            else if (parts.Length == 2)
            {
                customer.Street = parts[0].Trim();
                customer.City = parts[1].Trim();
            }
            else
            {
                customer.Street = customer.Address.Trim();
            }
        }

        existing.CustomerCode = customer.CustomerCode;
        existing.FirstName = customer.FirstName;
        existing.LastName = customer.LastName;
        existing.Street = customer.Street;
        existing.City = customer.City;
        existing.Province = customer.Province;
        existing.ContactNumber = customer.ContactNumber;
        existing.EmailAddress = customer.EmailAddress;
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