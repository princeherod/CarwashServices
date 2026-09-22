using CRM.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CRM.api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly MasterErpDbContext _db;

    public AuthController(MasterErpDbContext db)
    {
        _db = db;
    }

    public class LoginRequest
    {
        public string Email { get; set; } = "";
        public string Password { get; set; } = "";
    }

    // POST: api/auth/login
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Email) ||
            string.IsNullOrWhiteSpace(req.Password))
        {
            return BadRequest(new { message = "Email and password are required." });
        }

        var email = req.Email.Trim().ToLowerInvariant();

        // Case-insensitive email lookup so "Manager@x.com" and "manager@x.com" both work.
        var user = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email.ToLower() == email);

        if (user is null)
            return Unauthorized(new { message = "Invalid email or password." });

        // Inactive accounts cannot log in
        if (!string.Equals(user.Status, "Active", StringComparison.OrdinalIgnoreCase))
            return Unauthorized(new { message = "This account is inactive. Contact your administrator." });

        // Seeded users may have an empty hash — accept any non-empty password for them.
        // Users created via Manage Users store the password in PasswordHash.
        // TODO: replace with proper hashing + verification.
        if (!string.IsNullOrWhiteSpace(user.PasswordHash))
        {
            if (user.PasswordHash != req.Password)
                return Unauthorized(new { message = "Invalid email or password." });
        }

        return Ok(new
        {
            userId = user.UserId,
            fullName = user.FullName,
            email = user.Email,
            roleId = user.RoleId
        });
    }
}