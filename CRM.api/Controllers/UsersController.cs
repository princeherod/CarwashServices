using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CRM.api.Services;
using CRM.domain.Entities;
using CRM.Infrastructure.Data;
using CRM.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CRM.api.Controllers;

[ApiController]
[Route("api/users")]
public class UsersController : ControllerBase
{
    private readonly MasterErpDbContext _db;
    private readonly ITenantDbContextFactory _tenantFactory;

    public UsersController(MasterErpDbContext db, ITenantDbContextFactory tenantFactory)
    {
        _db = db;
        _tenantFactory = tenantFactory;
    }

    // Default tenant roles managed on Manage Users: Admin (1,2), Manager (3), Service Staff (4)
    private static readonly int[] DefaultRoleIds = { 1, 2, 3, 4 };

    // GET: api/users?companyId=3&branchId=2
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? roleIds = null,
        [FromQuery] int? companyId = null,
        [FromQuery] int? branchId = null)
    {
        int targetCompanyId = companyId ?? 0;

        // 1. Resolve calling User ID
        int callingUserId = 0;
        if (Request.Headers.TryGetValue("X-Current-User-Id", out var hVal) && int.TryParse(hVal.FirstOrDefault(), out var uid) && uid > 0)
            callingUserId = uid;
        else if (Request.Headers.TryGetValue("X-User-Id", out var hVal2) && int.TryParse(hVal2.FirstOrDefault(), out var uid2) && uid2 > 0)
            callingUserId = uid2;

        User? callingUser = null;
        if (callingUserId > 0)
        {
            callingUser = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == callingUserId);
        }

        // 2. Tenant isolation enforcement
        if (callingUser != null && callingUser.RoleId != 4 && callingUser.CompanyId.HasValue)
        {
            if (targetCompanyId > 0 && targetCompanyId != callingUser.CompanyId.Value)
            {
                return StatusCode(403, new { message = $"Security Violation: Cross-tenant access forbidden. You cannot access company {targetCompanyId}'s users." });
            }
            targetCompanyId = callingUser.CompanyId.Value;
        }

        // 3. Branch access validation
        if (targetCompanyId > 0)
        {
            var branchCheck = await BranchSecurityHelper.ResolveAndValidateAsync(_db, HttpContext, targetCompanyId, branchId);
            if (!branchCheck.Allowed)
            {
                return StatusCode(403, new { message = branchCheck.ErrorMessage ?? "Forbidden: You are not authorized to access this branch." });
            }
            if (branchCheck.EffectiveBranchId.HasValue)
            {
                branchId = branchCheck.EffectiveBranchId.Value;
            }
        }

        // 4. Resolve branch metadata from tenant DB
        bool isMainBranch = false;
        Dictionary<int, string> branchNames = new();
        if (targetCompanyId > 0)
        {
            try
            {
                await using var tenant = await _tenantFactory.CreateAsync(targetCompanyId);
                var tenantBranches = await tenant.Branches.AsNoTracking().ToListAsync();
                foreach (var b in tenantBranches)
                {
                    branchNames[b.BranchId] = b.BranchName;
                }

                if (branchId.HasValue && branchId.Value > 0)
                {
                    var requestedBranch = tenantBranches.FirstOrDefault(b => b.BranchId == branchId.Value);
                    if (requestedBranch == null && tenantBranches.Count > 0)
                    {
                        return NotFound(new { message = $"Branch with ID {branchId.Value} does not exist for this company." });
                    }
                    if (requestedBranch != null)
                    {
                        isMainBranch = requestedBranch.IsMainBranch;
                    }
                }
            }
            catch
            {
                // Single-tenant or table not yet migrated
            }
        }

        // 5. Query users
        IQueryable<User> query = _db.Users.AsNoTracking();

        if (targetCompanyId > 0)
        {
            query = query.Where(u => u.CompanyId == targetCompanyId);
        }

        // Branch-scoped filtering
        if (branchId.HasValue && branchId.Value > 0)
        {
            int bId = branchId.Value;
            if (isMainBranch)
            {
                // Main Branch displays existing tenant users (assigned to Main Branch or legacy users with NULL BranchId)
                query = query.Where(u => u.BranchId == bId || u.BranchId == null);
            }
            else
            {
                // Any other branch (Calinan, Matina, etc.) strictly returns ONLY users assigned to that branch
                query = query.Where(u => u.BranchId == bId);
            }
        }

        // Role filtering
        if (!string.IsNullOrWhiteSpace(roleIds))
        {
            var ids = roleIds
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(s => int.TryParse(s, out var v) ? (int?)v : null)
                .Where(v => v.HasValue)
                .Select(v => v!.Value)
                .ToList();

            if (ids.Count > 0)
            {
                query = query.Where(u => ids.Contains(u.RoleId));
            }
        }
        else if (targetCompanyId > 0)
        {
            query = query.Where(u => DefaultRoleIds.Contains(u.RoleId));
        }

        var list = await query
            .OrderBy(u => u.UserId)
            .Select(u => new
            {
                u.UserId,
                u.FirstName,
                u.LastName,
                u.FullName,
                u.RoleId,
                u.Email,
                u.Status,
                u.CreatedAt,
                u.CompanyId,
                u.BranchId
            })
            .ToListAsync();

        var result = list.Select(u =>
        {
            string bName = "";
            if (u.BranchId.HasValue && branchNames.TryGetValue(u.BranchId.Value, out var bn))
            {
                bName = bn;
            }
            else if (!u.BranchId.HasValue && isMainBranch && branchNames.Count > 0)
            {
                bName = branchNames.Values.FirstOrDefault() ?? "Main Branch";
            }

            return new
            {
                u.UserId,
                u.FirstName,
                u.LastName,
                u.FullName,
                u.RoleId,
                u.Email,
                u.Status,
                u.CreatedAt,
                u.CompanyId,
                u.BranchId,
                BranchName = bName
            };
        });

        return Ok(result);
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

        string branchName = "";
        if (row.BranchId.HasValue && row.CompanyId.HasValue)
        {
            try
            {
                await using var tenant = await _tenantFactory.CreateAsync(row.CompanyId.Value);
                var b = await tenant.Branches.AsNoTracking().FirstOrDefaultAsync(x => x.BranchId == row.BranchId.Value);
                if (b != null) branchName = b.BranchName;
            }
            catch { }
        }

        return Ok(new
        {
            row.UserId,
            row.FirstName,
            row.LastName,
            row.FullName,
            row.RoleId,
            row.Email,
            row.Status,
            row.CreatedAt,
            row.CompanyId,
            row.BranchId,
            BranchName = branchName
        });
    }

    public class UserCreateRequest
    {
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? FullName { get; set; }
        public string Email { get; set; } = "";
        public int RoleId { get; set; } = 2; // Default to Admin (2)
        public string Status { get; set; } = "Active";
        public string? Password { get; set; }
        public int? CompanyId { get; set; }
        public int? BranchId { get; set; }
        public int? CurrentUserId { get; set; }
    }

    // POST: api/users
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] UserCreateRequest req)
    {
        var first = req.FirstName?.Trim();
        var last = req.LastName?.Trim();

        if (string.IsNullOrWhiteSpace(first) || string.IsNullOrWhiteSpace(last))
        {
            if (!string.IsNullOrWhiteSpace(req.FullName))
            {
                var full = req.FullName.Trim();
                var idx = full.IndexOf(' ');
                if (idx > 0)
                {
                    if (string.IsNullOrWhiteSpace(first)) first = full.Substring(0, idx).Trim();
                    if (string.IsNullOrWhiteSpace(last)) last = full.Substring(idx + 1).Trim();
                }
                else
                {
                    if (string.IsNullOrWhiteSpace(first)) first = full;
                    if (string.IsNullOrWhiteSpace(last)) last = "-";
                }
            }
        }

        if (string.IsNullOrWhiteSpace(first))
            return BadRequest(new { message = "First name is required." });
        if (string.IsNullOrWhiteSpace(last))
            return BadRequest(new { message = "Last name is required." });
        if (string.IsNullOrWhiteSpace(req.Email))
            return BadRequest(new { message = "Email is required." });
        if (req.RoleId <= 0)
            return BadRequest(new { message = "Valid Role is required." });
        if (string.IsNullOrWhiteSpace(req.Password))
            return BadRequest(new { message = "Password is required." });

        var email = req.Email.Trim().ToLowerInvariant();

        var exists = await _db.Users.AnyAsync(u => u.Email.ToLower() == email);
        if (exists)
            return Conflict(new { message = $"A user with email '{req.Email}' already exists." });

        // Resolve Company and caller permissions
        int targetCompanyId = req.CompanyId ?? 1;
        int callingUserId = req.CurrentUserId ?? 0;
        if (callingUserId == 0 && Request.Headers.TryGetValue("X-Current-User-Id", out var hVal) && int.TryParse(hVal.FirstOrDefault(), out var uid) && uid > 0)
            callingUserId = uid;

        if (callingUserId > 0)
        {
            var caller = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == callingUserId);
            if (caller != null && caller.RoleId != 4 && caller.CompanyId.HasValue)
            {
                targetCompanyId = caller.CompanyId.Value;
                if (caller.BranchId.HasValue && caller.BranchId.Value > 0)
                {
                    req.BranchId = caller.BranchId.Value;
                }
            }
        }

        // Validate BranchId if assigned
        int? assignedBranchId = (req.BranchId.HasValue && req.BranchId.Value > 0) ? req.BranchId.Value : null;
        if (assignedBranchId.HasValue && targetCompanyId > 0)
        {
            try
            {
                await using var tenant = await _tenantFactory.CreateAsync(targetCompanyId);
                var branchExists = await tenant.Branches.AnyAsync(b => b.BranchId == assignedBranchId.Value);
                if (!branchExists)
                {
                    return BadRequest(new { message = $"Branch ID {assignedBranchId.Value} does not exist for this company." });
                }
            }
            catch { }
        }

        var user = new User
        {
            FirstName = first,
            LastName = last,
            FullName = $"{first} {last}".Trim(),
            Email = req.Email.Trim(),
            RoleId = req.RoleId,
            CompanyId = targetCompanyId,
            BranchId = assignedBranchId,
            Status = string.IsNullOrWhiteSpace(req.Status) ? "Active" : req.Status,
            PasswordHash = req.Password ?? string.Empty,
            IdentityUserId = string.Empty,
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
            user.FirstName,
            user.LastName,
            user.FullName,
            user.RoleId,
            user.Email,
            user.Status,
            user.CreatedAt,
            user.CompanyId,
            user.BranchId
        });
    }

    public class UserUpdateRequest
    {
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? FullName { get; set; }
        public string Email { get; set; } = "";
        public int RoleId { get; set; }
        public string Status { get; set; } = "Active";
        public string? Password { get; set; }   // blank = keep existing
        public int? CompanyId { get; set; }
        public int? BranchId { get; set; }
        public int? CurrentUserId { get; set; }
    }

    // PUT: api/users/5
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UserUpdateRequest req)
    {
        var existing = await _db.Users.FirstOrDefaultAsync(u => u.UserId == id);
        if (existing is null)
            return NotFound(new { message = $"User {id} not found." });

        int currentUserId = 0;
        if (Request.Headers.TryGetValue("X-Current-User-Id", out var hVal) && int.TryParse(hVal, out var uid) && uid > 0)
        {
            currentUserId = uid;
        }
        else if (Request.Headers.TryGetValue("X-User-Id", out var hVal2) && int.TryParse(hVal2, out var uid2) && uid2 > 0)
        {
            currentUserId = uid2;
        }
        else if (req.CurrentUserId.HasValue && req.CurrentUserId.Value > 0)
        {
            currentUserId = req.CurrentUserId.Value;
        }
        else if (Request.Query.TryGetValue("currentUserId", out var qVal) && int.TryParse(qVal, out var qUid) && qUid > 0)
        {
            currentUserId = qUid;
        }

        bool isSelfEdit = (currentUserId > 0 && currentUserId == id);

        // BACKEND SECURITY: Prevent users from editing their own user account in Manage Users
        if (isSelfEdit)
        {
            return BadRequest(new { message = "You cannot edit your own user account from Manage Users." });
        }

        var first = req.FirstName?.Trim();
        var last = req.LastName?.Trim();

        if (string.IsNullOrWhiteSpace(first) || string.IsNullOrWhiteSpace(last))
        {
            if (!string.IsNullOrWhiteSpace(req.FullName))
            {
                var full = req.FullName.Trim();
                var idx = full.IndexOf(' ');
                if (idx > 0)
                {
                    if (string.IsNullOrWhiteSpace(first)) first = full.Substring(0, idx).Trim();
                    if (string.IsNullOrWhiteSpace(last)) last = full.Substring(idx + 1).Trim();
                }
                else
                {
                    if (string.IsNullOrWhiteSpace(first)) first = full;
                    if (string.IsNullOrWhiteSpace(last)) last = "-";
                }
            }
        }

        if (string.IsNullOrWhiteSpace(first))
            return BadRequest(new { message = "First name is required." });
        if (string.IsNullOrWhiteSpace(last))
            return BadRequest(new { message = "Last name is required." });
        if (string.IsNullOrWhiteSpace(req.Email))
            return BadRequest(new { message = "Email is required." });
        if (req.RoleId <= 0)
            return BadRequest(new { message = "Valid Role is required." });

        var email = req.Email.Trim().ToLowerInvariant();
        var dupe = await _db.Users.AnyAsync(u => u.UserId != id && u.Email.ToLower() == email);
        if (dupe)
            return Conflict(new { message = $"Another user already uses email '{req.Email}'." });

        existing.FirstName = first;
        existing.LastName = last;
        existing.FullName = $"{first} {last}".Trim();
        existing.Email = req.Email.Trim();
        existing.RoleId = req.RoleId;
        existing.Status = string.IsNullOrWhiteSpace(req.Status) ? "Active" : req.Status;
        if (req.CompanyId.HasValue)
            existing.CompanyId = req.CompanyId.Value;

        // User movement between branches
        if (req.BranchId.HasValue)
        {
            int targetCid = existing.CompanyId ?? 1;
            if (req.BranchId.Value > 0)
            {
                try
                {
                    await using var tenant = await _tenantFactory.CreateAsync(targetCid);
                    var bExists = await tenant.Branches.AnyAsync(b => b.BranchId == req.BranchId.Value);
                    if (!bExists)
                    {
                        return BadRequest(new { message = $"Branch ID {req.BranchId.Value} does not exist for this company." });
                    }
                }
                catch { }
                existing.BranchId = req.BranchId.Value;
            }
            else
            {
                existing.BranchId = null;
            }
        }

        if (!string.IsNullOrEmpty(req.Password))
            existing.PasswordHash = req.Password;

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
            existing.FirstName,
            existing.LastName,
            existing.FullName,
            existing.RoleId,
            existing.Email,
            existing.Status,
            existing.CreatedAt,
            existing.CompanyId,
            existing.BranchId
        });
    }

    // DELETE: api/users/5
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var existing = await _db.Users.FirstOrDefaultAsync(u => u.UserId == id);
        if (existing is null)
            return NotFound(new { message = $"User {id} not found." });

        if (existing.RoleId == 4)
            return BadRequest(new { message = "Super Admin accounts cannot be deleted." });

        _db.Users.Remove(existing);

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