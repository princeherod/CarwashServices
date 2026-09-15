using CRM.Domain.Entities;
using CRM.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CRM.api.Controllers;

[ApiController]
[Route("api/tenant/{companyId:int}/suppliers")]
public class SuppliersController : ControllerBase
{
    private readonly ITenantDbContextFactory _tenantFactory;

    public SuppliersController(ITenantDbContextFactory tenantFactory)
    {
        _tenantFactory = tenantFactory;
    }

    // GET: api/tenant/1/suppliers
    [HttpGet]
    public async Task<IActionResult> GetAll(int companyId)
    {
        await using var tenantDb = await _tenantFactory.CreateAsync(companyId);

        var suppliers = await tenantDb.Suppliers
            .AsNoTracking()
            .OrderBy(s => s.SupplierId)
            .ToListAsync();

        return Ok(suppliers);
    }

    // GET: api/tenant/1/suppliers/5
    [HttpGet("{supplierId:int}")]
    public async Task<IActionResult> GetById(int companyId, int supplierId)
    {
        await using var tenantDb = await _tenantFactory.CreateAsync(companyId);

        var supplier = await tenantDb.Suppliers
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.SupplierId == supplierId);

        if (supplier is null)
        {
            return NotFound(new
            {
                message = $"Supplier {supplierId} not found in tenant {companyId}."
            });
        }

        return Ok(supplier);
    }

    // POST: api/tenant/1/suppliers
    [HttpPost]
    public async Task<IActionResult> Create(int companyId, [FromBody] Supplier supplier)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        await using var tenantDb = await _tenantFactory.CreateAsync(companyId);

        var exists = await tenantDb.Suppliers
            .AnyAsync(s => s.SupplierCode == supplier.SupplierCode);

        if (exists)
        {
            return Conflict(new
            {
                message = $"A supplier with code '{supplier.SupplierCode}' already exists in tenant {companyId}."
            });
        }

        tenantDb.Suppliers.Add(supplier);
        await tenantDb.SaveChangesAsync();

        return CreatedAtAction(
            nameof(GetById),
            new { companyId, supplierId = supplier.SupplierId },
            supplier);
    }

    // DELETE: api/tenant/1/suppliers/5
    [HttpDelete("{supplierId:int}")]
    public async Task<IActionResult> Delete(int companyId, int supplierId)
    {
        await using var tenantDb = await _tenantFactory.CreateAsync(companyId);

        var supplier = await tenantDb.Suppliers
            .FirstOrDefaultAsync(s => s.SupplierId == supplierId);

        if (supplier is null)
        {
            return NotFound(new
            {
                message = $"Supplier {supplierId} not found in tenant {companyId}."
            });
        }

        tenantDb.Suppliers.Remove(supplier);
        await tenantDb.SaveChangesAsync();

        return NoContent();
    }
}