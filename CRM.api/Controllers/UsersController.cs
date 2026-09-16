using CRM.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CRM.api.Controllers;

[ApiController]
[Route("api/users")]
public class UsersController : ControllerBase
{
    private readonly MasterErpDbContext _db;

    public UsersController(MasterErpDbContext db)
    {
        _db = db;
    }

    // GET: api/users
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var list = await _db.Users
            .AsNoTracking()
            .OrderBy(u => u.UserId)
            .Select(u => new
            {
                u.UserId,
                u.FullName,
                u.RoleId,
                u.Email,
                u.Status
            })
            .ToListAsync();

        return Ok(list);
    }

    // GET: api/users/5
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var row = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.UserId == id);

        if (row is null)
            return NotFound(new { message = $"User {id} not found." });

        return Ok(row);
    }
}