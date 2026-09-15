using CRM.Domain.Entities;
using CRM.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CRM.api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CompaniesController : ControllerBase
{
    private readonly MasterErpDbContext _masterDb;

    public CompaniesController(MasterErpDbContext masterDb)
    {
        _masterDb = masterDb;
    }

    // GET: api/companies
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var companies = await _masterDb.Companies
            .AsNoTracking()
            .OrderBy(c => c.CompanyId)
            .ToListAsync();

        return Ok(companies);
    }

    // GET: api/companies/1
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var company = await _masterDb.Companies
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.CompanyId == id);

        if (company is null)
        {
            return NotFound(new { message = $"Company {id} not found." });
        }

        return Ok(company);
    }

    // POST: api/companies
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] Company company)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var exists = await _masterDb.Companies
            .AnyAsync(c => c.CompanyCode == company.CompanyCode);

        if (exists)
        {
            return Conflict(new
            {
                message = $"A company with code '{company.CompanyCode}' already exists."
            });
        }

        _masterDb.Companies.Add(company);
        await _masterDb.SaveChangesAsync();

        return CreatedAtAction(
            nameof(GetById),
            new { id = company.CompanyId },
            company);
    }

    // DELETE: api/companies/5
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var company = await _masterDb.Companies
            .FirstOrDefaultAsync(c => c.CompanyId == id);

        if (company is null)
        {
            return NotFound(new { message = $"Company {id} not found." });
        }

        _masterDb.Companies.Remove(company);
        await _masterDb.SaveChangesAsync();

        return NoContent();
    }
}