using CRM.Infrastructure.Data;
using CRM.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CRM.api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly MasterErpDbContext _db;
    private readonly ITenantDbContextFactory _tenantFactory;

    public AuthController(MasterErpDbContext db, ITenantDbContextFactory tenantFactory)
    {
        _db = db;
        _tenantFactory = tenantFactory;
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

        var user = await _db.Users
            .Include(u => u.Company)
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email.ToLower() == email);

        if (user is null)
            return Unauthorized(new { message = "Invalid email or password." });

        if (!string.Equals(user.Status, "Active", StringComparison.OrdinalIgnoreCase))
            return Unauthorized(new { message = "This account is inactive. Contact your administrator." });

        // Seeded users have an empty PasswordHash — accept any non-empty password for them.
        // Users created via Manage Users store the password in PasswordHash.
        // TODO: replace with proper hashing.
        if (!string.IsNullOrWhiteSpace(user.PasswordHash))
        {
            if (user.PasswordHash != req.Password)
                return Unauthorized(new { message = "Invalid email or password." });
        }

        bool termsAccepted = true;
        string? termsAcceptedVersion = null;
        DateTime? termsAcceptedAt = null;

        bool multiBranchEnabled = false;
        if (user.Company != null)
        {
            termsAccepted = user.Company.TermsAccepted;
            termsAcceptedVersion = user.Company.TermsAcceptedVersion;
            termsAcceptedAt = user.Company.TermsAcceptedAt;
        }

        if (user.CompanyId.HasValue && user.CompanyId.Value > 0)
        {
            var sub = await _db.TenantSubscriptions
                .Include(s => s.Plan)
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.CompanyId == user.CompanyId.Value && s.Status == "Active");

            if (sub?.Plan != null)
            {
                multiBranchEnabled = sub.Plan.MultiBranchEnabled;
            }
        }

        string branchName = "";
        if (user.BranchId.HasValue && user.BranchId.Value > 0 && user.CompanyId.HasValue && user.CompanyId.Value > 0)
        {
            try
            {
                await using var tenant = await _tenantFactory.CreateAsync(user.CompanyId.Value);
                var br = await tenant.Branches.AsNoTracking().FirstOrDefaultAsync(b => b.BranchId == user.BranchId.Value);
                if (br != null)
                {
                    branchName = br.BranchName;
                }
            }
            catch { }
        }

        return Ok(new
        {
            userId = user.UserId,
            firstName = user.FirstName,
            lastName = user.LastName,
            fullName = user.FullName,
            email = user.Email,
            roleId = user.RoleId,
            companyId = user.CompanyId,
            companyName = user.Company?.CompanyName ?? "",
            companyCode = user.Company?.CompanyCode ?? "",
            branchId = user.BranchId,
            branchName = branchName,
            termsAccepted = termsAccepted,
            termsAcceptedVersion = termsAcceptedVersion,
            termsAcceptedAt = termsAcceptedAt,
            multiBranchEnabled = multiBranchEnabled
        });
    }
}