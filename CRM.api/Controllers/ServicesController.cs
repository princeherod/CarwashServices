using CRM.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CRM.api.Controllers;

[ApiController]
[Route("api/services")]
public class ServicesController : ControllerBase
{
    private readonly MasterErpDbContext _db;

    public ServicesController(MasterErpDbContext db)
    {
        _db = db;
    }

    // GET: api/services
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var list = await _db.Services
            .AsNoTracking()
            .OrderBy(s => s.ServiceId)
            .Select(s => new
            {
                s.ServiceId,
                s.ServiceName,
                s.Price,
                s.DurationMinutes,
                s.Description
            })
            .ToListAsync();

        return Ok(list);
    }

    // GET: api/services/5
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var row = await _db.Services
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.ServiceId == id);

        if (row is null)
            return NotFound(new { message = $"Service {id} not found." });

        return Ok(row);
    }
}