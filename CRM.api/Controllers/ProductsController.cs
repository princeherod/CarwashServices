using CRM.Domain.Entities;
using CRM.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CRM.api.Controllers;

[ApiController]
[Route("api/tenant/{companyId:int}/products")]
public class ProductsController : ControllerBase
{
    private readonly ITenantDbContextFactory _tenantFactory;

    public ProductsController(ITenantDbContextFactory tenantFactory)
    {
        _tenantFactory = tenantFactory;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(int companyId)
    {
        await using var tenantDb = await _tenantFactory.CreateAsync(companyId);
        var products = await tenantDb.Products
            .AsNoTracking()
            .Where(p => !p.IsArchived)
            .OrderBy(p => p.ProductId)
            .ToListAsync();
        return Ok(products);
    }

    [HttpGet("archived")]
    public async Task<IActionResult> GetArchived(int companyId)
    {
        await using var tenantDb = await _tenantFactory.CreateAsync(companyId);
        var products = await tenantDb.Products
            .AsNoTracking()
            .Where(p => p.IsArchived)
            .OrderByDescending(p => p.ArchivedAt)
            .ToListAsync();
        return Ok(products);
    }

    [HttpGet("{productId:int}")]
    public async Task<IActionResult> GetById(int companyId, int productId)
    {
        await using var tenantDb = await _tenantFactory.CreateAsync(companyId);
        var product = await tenantDb.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.ProductId == productId);

        if (product is null)
            return NotFound(new { message = $"Product {productId} not found in tenant {companyId}." });

        return Ok(product);
    }

    [HttpPost]
    public async Task<IActionResult> Create(int companyId, [FromBody] Product product)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        await using var tenantDb = await _tenantFactory.CreateAsync(companyId);

        var exists = await tenantDb.Products.AnyAsync(p => p.ProductCode == product.ProductCode);
        if (exists)
            return Conflict(new { message = $"A product with code '{product.ProductCode}' already exists." });

        product.IsArchived = false;
        product.ArchivedAt = null;
        product.ArchivedBy = null;

        tenantDb.Products.Add(product);
        await tenantDb.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { companyId, productId = product.ProductId }, product);
    }

    [HttpPut("{productId:int}")]
    public async Task<IActionResult> Update(int companyId, int productId, [FromBody] Product product)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        await using var tenantDb = await _tenantFactory.CreateAsync(companyId);
        var existing = await tenantDb.Products.FirstOrDefaultAsync(p => p.ProductId == productId);

        if (existing is null)
            return NotFound(new { message = $"Product {productId} not found in tenant {companyId}." });

        existing.ProductCode = product.ProductCode;
        existing.ProductName = product.ProductName;
        existing.Description = product.Description;
        existing.UnitPrice = product.UnitPrice;
        existing.DurationMinutes = product.DurationMinutes;
        existing.Category = product.Category;
        existing.IsActive = product.IsActive;

        await tenantDb.SaveChangesAsync();
        return Ok(existing);
    }

    public class ArchiveRequest { public string? ArchivedBy { get; set; } }

    [HttpPut("{productId:int}/archive")]
    public async Task<IActionResult> Archive(int companyId, int productId, [FromBody] ArchiveRequest? req)
    {
        await using var tenantDb = await _tenantFactory.CreateAsync(companyId);
        var existing = await tenantDb.Products.FirstOrDefaultAsync(p => p.ProductId == productId);

        if (existing is null)
            return NotFound(new { message = $"Product {productId} not found." });
        if (existing.IsArchived)
            return Conflict(new { message = "This service is already archived." });

        existing.IsArchived = true;
        existing.ArchivedAt = DateTime.UtcNow;
        existing.ArchivedBy = string.IsNullOrWhiteSpace(req?.ArchivedBy) ? "Admin" : req!.ArchivedBy;

        await tenantDb.SaveChangesAsync();
        return Ok(existing);
    }

    [HttpPut("{productId:int}/restore")]
    public async Task<IActionResult> Restore(int companyId, int productId)
    {
        await using var tenantDb = await _tenantFactory.CreateAsync(companyId);
        var existing = await tenantDb.Products.FirstOrDefaultAsync(p => p.ProductId == productId);

        if (existing is null)
            return NotFound(new { message = $"Product {productId} not found." });
        if (!existing.IsArchived)
            return Conflict(new { message = "This service is not archived." });

        existing.IsArchived = false;
        existing.ArchivedAt = null;
        existing.ArchivedBy = null;

        await tenantDb.SaveChangesAsync();
        return Ok(existing);
    }

    [HttpDelete("{productId:int}")]
    public async Task<IActionResult> Delete(int companyId, int productId)
    {
        await using var tenantDb = await _tenantFactory.CreateAsync(companyId);
        var product = await tenantDb.Products.FirstOrDefaultAsync(p => p.ProductId == productId);

        if (product is null)
            return NotFound(new { message = $"Product {productId} not found." });

        tenantDb.Products.Remove(product);
        await tenantDb.SaveChangesAsync();
        return NoContent();
    }
}