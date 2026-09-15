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

    // GET: api/tenant/1/products
    [HttpGet]
    public async Task<IActionResult> GetAll(int companyId)
    {
        await using var tenantDb = await _tenantFactory.CreateAsync(companyId);

        var products = await tenantDb.Products
            .AsNoTracking()
            .OrderBy(p => p.ProductId)
            .ToListAsync();

        return Ok(products);
    }

    // GET: api/tenant/1/products/5
    [HttpGet("{productId:int}")]
    public async Task<IActionResult> GetById(int companyId, int productId)
    {
        await using var tenantDb = await _tenantFactory.CreateAsync(companyId);

        var product = await tenantDb.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.ProductId == productId);

        if (product is null)
        {
            return NotFound(new { message = $"Product {productId} not found in tenant {companyId}." });
        }

        return Ok(product);
    }

    // POST: api/tenant/1/products
    [HttpPost]
    public async Task<IActionResult> Create(int companyId, [FromBody] Product product)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        await using var tenantDb = await _tenantFactory.CreateAsync(companyId);

        var exists = await tenantDb.Products
            .AnyAsync(p => p.ProductCode == product.ProductCode);

        if (exists)
        {
            return Conflict(new
            {
                message = $"A product with code '{product.ProductCode}' already exists in tenant {companyId}."
            });
        }

        tenantDb.Products.Add(product);
        await tenantDb.SaveChangesAsync();

        return CreatedAtAction(
            nameof(GetById),
            new { companyId, productId = product.ProductId },
            product);
    }

    // DELETE: api/tenant/1/products/5
    [HttpDelete("{productId:int}")]
    public async Task<IActionResult> Delete(int companyId, int productId)
    {
        await using var tenantDb = await _tenantFactory.CreateAsync(companyId);

        var product = await tenantDb.Products
            .FirstOrDefaultAsync(p => p.ProductId == productId);

        if (product is null)
        {
            return NotFound(new { message = $"Product {productId} not found in tenant {companyId}." });
        }

        tenantDb.Products.Remove(product);
        await tenantDb.SaveChangesAsync();

        return NoContent();
    }
}