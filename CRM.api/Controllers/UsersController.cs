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

        if (row is null || !AllowedRoleIds.Contains(row.RoleId))
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
        if (string.IsNullOrWhiteSpace(req.Password))
            return BadRequest(new { message = "Password is required." });

        var email = req.Email.Trim().ToLowerInvariant();

        var exists = await _db.Users.AnyAsync(u => u.Email.ToLower() == email);
        if (exists)
            return Conflict(new { message = $"A user with email '{req.Email}' already exists." });

        var user = new User
        {
            FullName = req.FullName.Trim(),
            Email = req.Email.Trim(),
            RoleId = req.RoleId,
            Status = string.IsNullOrWhiteSpace(req.Status) ? "Active" : req.Status,
            PasswordHash = req.Password ?? string.Empty,   // never null in the DB
            IdentityUserId = string.Empty,                 // never null in the DB
            CreatedAt = DateTime.UtcNow
        };

        _db.Users.Add(user);

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            Exception? deepest = ex;
            while (deepest.InnerException != null) deepest = deepest.InnerException;
            var msg = deepest.Message;

            if (msg.Contains("FOREIGN KEY", StringComparison.OrdinalIgnoreCase)
                || msg.Contains("conflicted", StringComparison.OrdinalIgnoreCase))
            {
                return Conflict(new
                {
                    message = "Role does not exist in the Roles table.",
                    detail = msg,
                    hint = "Insert the missing row into Roles with the same RoleId."
                });
            }

            return StatusCode(500, new { message = "Create failed.", detail = msg });
        }

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
        public string? Password { get; set; }   // blank = keep existing
    }

    // PUT: api/users/5
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UserUpdateRequest req)
    {
        var existing = await _db.Users.FirstOrDefaultAsync(u => u.UserId == id);
        if (existing is null || !AllowedRoleIds.Contains(existing.RoleId))
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

        // Only replace the password if the caller actually sent a non-empty one.
        if (!string.IsNullOrEmpty(req.Password))
            existing.PasswordHash = req.Password;

        // ---- Defensive: the entity declares these as non-nullable strings,
        // but the DB rows may have drifted to NULL. Coerce before saving so
        // the UPDATE never fails on a NOT NULL constraint.
        existing.PasswordHash ??= string.Empty;
        existing.IdentityUserId ??= string.Empty;
        if (existing.CreatedAt == default)
            existing.CreatedAt = DateTime.UtcNow;

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            var inner = ex.InnerException?.Message ?? ex.Message;
            return StatusCode(500, new { message = "Save failed.", detail = inner });
        }

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
        if (row is null || !AllowedRoleIds.Contains(row.RoleId))
            return NotFound(new { message = $"User {id} not found." });

        _db.Users.Remove(row);

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            var inner = ex.InnerException?.Message ?? ex.Message;
            return StatusCode(500, new { message = "Delete failed.", detail = inner });
        }

        return NoContent();
    }
}