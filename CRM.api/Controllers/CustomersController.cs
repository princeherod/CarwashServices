using CRM.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CRM.api.Controllers;

[ApiController]
[Route("api/customers")]
public class CustomersController : ControllerBase
{
    private readonly MasterErpDbContext _db;

    public CustomersController(MasterErpDbContext db)
    {
        _db = db;
    }

    // GET: api/customers
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var list = await _db.Customers
            .AsNoTracking()
            .OrderBy(c => c.CustomerId)
            .Select(c => new
            {
                c.CustomerId,
                c.FirstName,
                c.LastName,
                c.FullName,
                c.Phone,
                c.Email,
                c.AddressLine,
                c.City,
                c.State,
                c.PostalCode,
                c.Address,
                c.CreatedAt
            })
            .ToListAsync();

        return Ok(list);
    }

    // GET: api/customers/5
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var row = await _db.Customers
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.CustomerId == id);

        if (row is null)
            return NotFound(new { message = $"Customer {id} not found." });

        return Ok(row);
    }
}