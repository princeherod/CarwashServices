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

        // Find user by email
        var user = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email == req.Email);

        if (user is null)
            return Unauthorized(new { message = "Invalid email or password." });

        // NOTE: your seeded users have empty PasswordHash.
        // For the demo, accept any non-empty password for seeded users.
        // Replace with real password verification later.
        if (!string.IsNullOrWhiteSpace(user.PasswordHash))
        {
            // TODO: hash + compare
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