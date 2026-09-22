using CRM.domain.Entities;
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

    // Roles allowed on the Manage Users screen.
    // Super Admin (1) and Admin (2) are intentionally excluded.
    private static readonly int[] AllowedRoleIds = { 3, 4 };   // 3=Service Staff, 4=Manager

    // GET: api/users
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var list = await _db.Users
            .AsNoTracking()
            .Where(u => AllowedRoleIds.Contains(u.RoleId))
            .OrderBy(u => u.UserId)
            .Select(u => new
            {
                u.UserId,
                u.FullName,
                u.RoleId,
                u.Email,
                u.Status,
                u.CreatedAt
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

        if (!AllowedRoleIds.Contains(row.RoleId))
            return NotFound(new { message = $"User {id} not found." });

        return Ok(new
        {
            row.UserId,
            row.FullName,
            row.RoleId,
            row.Email,
            row.Status,
            row.CreatedAt
        });
    }

    public class UserCreateRequest
    {
        public string FullName { get; set; } = "";
        public string Email { get; set; } = "";
        public int RoleId { get; set; }
        public string Status { get; set; } = "Active";
        public string? Password { get; set; }
    }

    // POST: api/users
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] UserCreateRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.FullName))
            return BadRequest(new { message = "Full name is required." });
        if (string.IsNullOrWhiteSpace(req.Email))
            return BadRequest(new { message = "Email is required." });
        if (!AllowedRoleIds.Contains(req.RoleId))
            return BadRequest(new { message = "Role must be Manager or Service Staff." });

        var email = req.Email.Trim().ToLowerInvariant();

        var exists = await _db.Users.AnyAsync(u => u.Email.ToLower() == email);
        if (exists)
            return Conflict(new { message = $"A user with email '{req.Email}' already exists." });

        // Demo hashing — replace with a real hash later.
        var passwordHash = string.IsNullOrWhiteSpace(req.Password) ? "" : req.Password;

        var user = new User
        {
            FullName = req.FullName.Trim(),
            Email = req.Email.Trim(),
            RoleId = req.RoleId,
            Status = string.IsNullOrWhiteSpace(req.Status) ? "Active" : req.Status,
            PasswordHash = passwordHash,
            IdentityUserId = "",      // linked later if you add ASP.NET Identity
            CreatedAt = DateTime.UtcNow
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = user.UserId }, new
        {
            user.UserId,
            user.FullName,
            user.RoleId,
            user.Email,
            user.Status,
            user.CreatedAt
        });
    }

    public class UserUpdateRequest
    {
        public string FullName { get; set; } = "";
        public string Email { get; set; } = "";
        public int RoleId { get; set; }
        public string Status { get; set; } = "Active";
        public string? Password { get; set; }     // optional — blank = keep existing
    }

    // PUT: api/users/5
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UserUpdateRequest req)
    {
        var existing = await _db.Users.FirstOrDefaultAsync(u => u.UserId == id);
        if (existing is null)
            return NotFound(new { message = $"User {id} not found." });

        if (!AllowedRoleIds.Contains(existing.RoleId))
            return NotFound(new { message = $"User {id} not found." });

        if (string.IsNullOrWhiteSpace(req.FullName))
            return BadRequest(new { message = "Full name is required." });
        if (string.IsNullOrWhiteSpace(req.Email))
            return BadRequest(new { message = "Email is required." });
        if (!AllowedRoleIds.Contains(req.RoleId))
            return BadRequest(new { message = "Role must be Manager or Service Staff." });

        var email = req.Email.Trim().ToLowerInvariant();
        var dupe = await _db.Users.AnyAsync(u => u.UserId != id && u.Email.ToLower() == email);
        if (dupe)
            return Conflict(new { message = $"Another user already uses email '{req.Email}'." });

        existing.FullName = req.FullName.Trim();
        existing.Email = req.Email.Trim();
        existing.RoleId = req.RoleId;
        existing.Status = string.IsNullOrWhiteSpace(req.Status) ? "Active" : req.Status;

        if (!string.IsNullOrWhiteSpace(req.Password))
            existing.PasswordHash = req.Password;   // replace with hashing later

        await _db.SaveChangesAsync();

        return Ok(new
        {
            existing.UserId,
            existing.FullName,
            existing.RoleId,
            existing.Email,
            existing.Status,
            existing.CreatedAt
        });
    }

    // DELETE: api/users/5
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var row = await _db.Users.FirstOrDefaultAsync(u => u.UserId == id);
        if (row is null)
            return NotFound(new { message = $"User {id} not found." });

        if (!AllowedRoleIds.Contains(row.RoleId))
            return NotFound(new { message = $"User {id} not found." });

        _db.Users.Remove(row);
        await _db.SaveChangesAsync();

        return NoContent();
    }
}