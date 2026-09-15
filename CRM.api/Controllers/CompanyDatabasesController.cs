using CRM.Domain.Entities;
using CRM.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CRM.api.Controllers;

[ApiController]
[Route("api/company-databases")]
public class CompanyDatabasesController : ControllerBase
{
    private readonly MasterErpDbContext _masterDb;

    public CompanyDatabasesController(MasterErpDbContext masterDb)
    {
        _masterDb = masterDb;
    }

    // GET: api/company-databases
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var rows = await _masterDb.CompanyDatabases
            .AsNoTracking()
            .OrderBy(d => d.CompanyDatabaseId)
            .ToListAsync();

        return Ok(rows);
    }

    // GET: api/company-databases/1
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var row = await _masterDb.CompanyDatabases
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.CompanyDatabaseId == id);

        if (row is null)
        {
            return NotFound(new { message = $"CompanyDatabase {id} not found." });
        }

        return Ok(row);
    }

    // POST: api/company-databases
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CompanyDatabase companyDatabase)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var exists = await _masterDb.CompanyDatabases
            .AnyAsync(d => d.CompanyId == companyDatabase.CompanyId
                        && d.DatabaseName == companyDatabase.DatabaseName);

        if (exists)
        {
            return Conflict(new
            {
                message = $"A database mapping for company {companyDatabase.CompanyId} with name '{companyDatabase.DatabaseName}' already exists."
            });
        }

        _masterDb.CompanyDatabases.Add(companyDatabase);
        await _masterDb.SaveChangesAsync();

        return CreatedAtAction(
            nameof(GetById),
            new { id = companyDatabase.CompanyDatabaseId },
            companyDatabase);
    }

    // DELETE: api/company-databases/5
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var row = await _masterDb.CompanyDatabases
            .FirstOrDefaultAsync(d => d.CompanyDatabaseId == id);

        if (row is null)
        {
            return NotFound(new { message = $"CompanyDatabase {id} not found." });
        }

        _masterDb.CompanyDatabases.Remove(row);
        await _masterDb.SaveChangesAsync();

        return NoContent();
    }
}