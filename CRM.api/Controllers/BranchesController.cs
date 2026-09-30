using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CRM.domain.Entities;
using CRM.Domain.Entities;
using CRM.Infrastructure.Data;
using CRM.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CRM.api.Controllers;

[ApiController]
[Route("api/branches")]
[Route("api/tenant/{companyId}/branches")]
public class BranchesController : ControllerBase
{
    private readonly MasterErpDbContext _masterDb;
    private readonly ITenantDbContextFactory _tenantFactory;

    public BranchesController(MasterErpDbContext masterDb, ITenantDbContextFactory tenantFactory)
    {
        _masterDb = masterDb;
        _tenantFactory = tenantFactory;
    }

    private int ResolveCompanyId(int? companyId = null)
    {
        if (companyId.HasValue && companyId.Value > 0)
            return companyId.Value;

        if (RouteData.Values.TryGetValue("companyId", out var rVal) &&
            int.TryParse(rVal?.ToString(), out var rId) && rId > 0)
            return rId;

        if (Request.Query.TryGetValue("companyId", out var qVal) &&
            int.TryParse(qVal.FirstOrDefault(), out var qId) && qId > 0)
            return qId;

        if (Request.Headers.TryGetValue("X-Company-Id", out var hVal) &&
            int.TryParse(hVal.FirstOrDefault(), out var hId) && hId > 0)
            return hId;

        return 1;
    }

    private async Task<(bool Allowed, string PlanName)> CheckMultiBranchCapabilityAsync(int companyId)
    {
        var subscription = await _masterDb.TenantSubscriptions
            .Include(s => s.Plan)
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.CompanyId == companyId && s.Status == "Active");

        if (subscription?.Plan != null)
        {
            return (subscription.Plan.MultiBranchEnabled, subscription.Plan.PlanName);
        }

        return (false, "None");
    }

    // GET: api/tenant/{companyId}/branches/capability
    [HttpGet("capability")]
    public async Task<IActionResult> GetCapability([FromRoute] int? companyId = null)
    {
        var cid = ResolveCompanyId(companyId);
        var (allowed, planName) = await CheckMultiBranchCapabilityAsync(cid);

        await using var tenant = await _tenantFactory.CreateAsync(cid);
        var activeCount = await tenant.Branches.CountAsync(b => !b.IsArchived && b.IsActive);

        return Ok(new
        {
            companyId = cid,
            multiBranchEnabled = allowed,
            planName = planName,
            activeBranchesCount = activeCount
        });
    }

    // GET: api/tenant/{companyId}/branches
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromRoute] int? companyId = null,
        [FromQuery] bool includeArchived = false)
    {
        var cid = ResolveCompanyId(companyId);
        await using var tenant = await _tenantFactory.CreateAsync(cid);

        var query = tenant.Branches.AsNoTracking();
        if (!includeArchived)
        {
            query = query.Where(b => !b.IsArchived);
        }

        var branches = await query
            .OrderByDescending(b => b.IsMainBranch)
            .ThenBy(b => b.BranchName)
            .ToListAsync();

        // Get count of requests & customers per branch
        var requestCounts = await tenant.ServiceRequests
            .AsNoTracking()
            .Where(r => !r.IsArchived && r.BranchId.HasValue)
            .GroupBy(r => r.BranchId!.Value)
            .Select(g => new { BranchId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.BranchId, x => x.Count);

        var customerCounts = await tenant.TenantCustomers
            .AsNoTracking()
            .Where(c => !c.IsArchived && c.BranchId.HasValue)
            .GroupBy(c => c.BranchId!.Value)
            .Select(g => new { BranchId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.BranchId, x => x.Count);

        // Fetch assigned administrators from MasterDb for this tenant company
        var assignedUsers = await _masterDb.Users
            .AsNoTracking()
            .Where(u => u.CompanyId == cid && u.BranchId.HasValue && u.Status == "Active")
            .OrderBy(u => u.RoleId)
            .ToListAsync();

        var adminDict = assignedUsers
            .GroupBy(u => u.BranchId!.Value)
            .ToDictionary(g => g.Key, g => g.First());

        var result = branches.Select(b => new
        {
            b.BranchId,
            b.BranchCode,
            b.BranchName,
            b.Address,
            b.City,
            b.Province,
            b.ContactNumber,
            b.Email,
            b.IsMainBranch,
            b.IsActive,
            b.IsArchived,
            b.ArchivedAt,
            b.ArchivedBy,
            b.CreatedAt,
            serviceRequestsCount = requestCounts.TryGetValue(b.BranchId, out var rc) ? rc : 0,
            customersCount = customerCounts.TryGetValue(b.BranchId, out var cc) ? cc : 0,
            assignedAdminId = adminDict.TryGetValue(b.BranchId, out var adm) ? (int?)adm.UserId : null,
            assignedAdminName = adminDict.TryGetValue(b.BranchId, out var adm2) ? adm2.FullName : "Unassigned",
            assignedAdminEmail = adminDict.TryGetValue(b.BranchId, out var adm3) ? adm3.Email : ""
        });

        return Ok(result);
    }

    // GET: api/tenant/{companyId}/branches/{id}
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(
        int id,
        [FromRoute] int? companyId = null)
    {
        var cid = ResolveCompanyId(companyId);
        await using var tenant = await _tenantFactory.CreateAsync(cid);

        var b = await tenant.Branches.AsNoTracking().FirstOrDefaultAsync(x => x.BranchId == id);
        if (b == null)
            return NotFound(new { message = $"Branch with ID {id} not found." });

        var reqCount = await tenant.ServiceRequests.CountAsync(r => !r.IsArchived && r.BranchId == id);
        var custCount = await tenant.TenantCustomers.CountAsync(c => !c.IsArchived && c.BranchId == id);

        var assignedAdmin = await _masterDb.Users
            .AsNoTracking()
            .Where(u => u.CompanyId == cid && u.BranchId == id && u.Status == "Active")
            .OrderBy(u => u.RoleId)
            .FirstOrDefaultAsync();

        return Ok(new
        {
            b.BranchId,
            b.BranchCode,
            b.BranchName,
            b.Address,
            b.City,
            b.Province,
            b.ContactNumber,
            b.Email,
            b.IsMainBranch,
            b.IsActive,
            b.IsArchived,
            b.ArchivedAt,
            b.ArchivedBy,
            b.CreatedAt,
            serviceRequestsCount = reqCount,
            customersCount = custCount,
            assignedAdminId = assignedAdmin?.UserId,
            assignedAdminName = assignedAdmin?.FullName ?? "Unassigned",
            assignedAdminEmail = assignedAdmin?.Email ?? ""
        });
    }

    public class BranchRequest
    {
        public string BranchCode { get; set; } = string.Empty;
        public string BranchName { get; set; } = string.Empty;
        public string? Address { get; set; }
        public string? City { get; set; }
        public string? Province { get; set; }
        public string? ContactNumber { get; set; }
        public string? Email { get; set; }
        public bool IsMainBranch { get; set; }
        public bool IsActive { get; set; } = true;
    }

    // POST: api/tenant/{companyId}/branches
    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] BranchRequest req,
        [FromRoute] int? companyId = null)
    {
        var cid = ResolveCompanyId(companyId);

        // Subscription check: only businesses whose subscription plan supports multi-branch can create branches
        var (allowed, planName) = await CheckMultiBranchCapabilityAsync(cid);
        if (!allowed)
        {
            return StatusCode(403, new
            {
                message = "Your current subscription plan does not support multiple branches. Please upgrade your plan to access Multi-Branch functionality."
            });
        }

        if (string.IsNullOrWhiteSpace(req.BranchName))
            return BadRequest(new { message = "Branch Name is required." });

        if (string.IsNullOrWhiteSpace(req.BranchCode))
            return BadRequest(new { message = "Branch Code is required." });

        await using var tenant = await _tenantFactory.CreateAsync(cid);

        var codeExists = await tenant.Branches.AnyAsync(b => b.BranchCode.ToLower() == req.BranchCode.Trim().ToLower());
        if (codeExists)
            return BadRequest(new { message = $"Branch Code '{req.BranchCode}' is already in use." });

        var nameExists = await tenant.Branches.AnyAsync(b => b.BranchName.ToLower() == req.BranchName.Trim().ToLower());
        if (nameExists)
            return BadRequest(new { message = $"Branch Name '{req.BranchName}' already exists." });

        // If this branch is marked as Main Branch, unmark any existing main branch
        if (req.IsMainBranch)
        {
            var existingMains = await tenant.Branches.Where(b => b.IsMainBranch).ToListAsync();
            foreach (var mb in existingMains)
            {
                mb.IsMainBranch = false;
            }
        }
        else
        {
            // If no branches exist yet, force first branch to be Main Branch
            var anyBranches = await tenant.Branches.AnyAsync();
            if (!anyBranches)
            {
                req.IsMainBranch = true;
            }
        }

        var branch = new TenantBranch
        {
            BranchCode = req.BranchCode.Trim().ToUpperInvariant(),
            BranchName = req.BranchName.Trim(),
            Address = req.Address?.Trim(),
            City = req.City?.Trim(),
            Province = req.Province?.Trim(),
            ContactNumber = req.ContactNumber?.Trim(),
            Email = req.Email?.Trim(),
            IsMainBranch = req.IsMainBranch,
            IsActive = req.IsActive,
            CreatedAt = DateTime.UtcNow
        };

        tenant.Branches.Add(branch);
        await tenant.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = branch.BranchId, companyId = cid }, branch);
    }

    // PUT: api/tenant/{companyId}/branches/{id}
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(
        int id,
        [FromBody] BranchRequest req,
        [FromRoute] int? companyId = null)
    {
        var cid = ResolveCompanyId(companyId);
        await using var tenant = await _tenantFactory.CreateAsync(cid);

        var branch = await tenant.Branches.FirstOrDefaultAsync(b => b.BranchId == id);
        if (branch == null)
            return NotFound(new { message = $"Branch with ID {id} not found." });

        if (string.IsNullOrWhiteSpace(req.BranchName))
            return BadRequest(new { message = "Branch Name is required." });

        if (string.IsNullOrWhiteSpace(req.BranchCode))
            return BadRequest(new { message = "Branch Code is required." });

        var codeExists = await tenant.Branches.AnyAsync(b => b.BranchId != id && b.BranchCode.ToLower() == req.BranchCode.Trim().ToLower());
        if (codeExists)
            return BadRequest(new { message = $"Branch Code '{req.BranchCode}' is already in use by another branch." });

        var nameExists = await tenant.Branches.AnyAsync(b => b.BranchId != id && b.BranchName.ToLower() == req.BranchName.Trim().ToLower());
        if (nameExists)
            return BadRequest(new { message = $"Branch Name '{req.BranchName}' already exists." });

        if (req.IsMainBranch && !branch.IsMainBranch)
        {
            var otherMains = await tenant.Branches.Where(b => b.BranchId != id && b.IsMainBranch).ToListAsync();
            foreach (var om in otherMains)
            {
                om.IsMainBranch = false;
            }
        }

        branch.BranchCode = req.BranchCode.Trim().ToUpperInvariant();
        branch.BranchName = req.BranchName.Trim();
        branch.Address = req.Address?.Trim();
        branch.City = req.City?.Trim();
        branch.Province = req.Province?.Trim();
        branch.ContactNumber = req.ContactNumber?.Trim();
        branch.Email = req.Email?.Trim();
        branch.IsMainBranch = req.IsMainBranch;
        branch.IsActive = req.IsActive;

        await tenant.SaveChangesAsync();

        return Ok(branch);
    }

    public class ArchiveRequest
    {
        public string? ArchivedBy { get; set; }
    }

    // POST: api/tenant/{companyId}/branches/{id}/archive
    [HttpPost("{id:int}/archive")]
    public async Task<IActionResult> Archive(
        int id,
        [FromBody] ArchiveRequest? req = null,
        [FromRoute] int? companyId = null)
    {
        var cid = ResolveCompanyId(companyId);
        await using var tenant = await _tenantFactory.CreateAsync(cid);

        var branch = await tenant.Branches.FirstOrDefaultAsync(b => b.BranchId == id);
        if (branch == null)
            return NotFound(new { message = $"Branch with ID {id} not found." });

        if (branch.IsMainBranch)
        {
            var otherActive = await tenant.Branches.AnyAsync(b => b.BranchId != id && !b.IsArchived && b.IsActive);
            if (!otherActive)
                return BadRequest(new { message = "Cannot archive the only active main branch." });
        }

        branch.IsArchived = true;
        branch.IsActive = false;
        branch.ArchivedAt = DateTime.UtcNow;
        branch.ArchivedBy = req?.ArchivedBy ?? "Admin";

        await tenant.SaveChangesAsync();

        return Ok(new { message = $"Branch '{branch.BranchName}' successfully archived." });
    }

    // POST: api/tenant/{companyId}/branches/{id}/restore
    [HttpPost("{id:int}/restore")]
    public async Task<IActionResult> Restore(
        int id,
        [FromRoute] int? companyId = null)
    {
        var cid = ResolveCompanyId(companyId);
        await using var tenant = await _tenantFactory.CreateAsync(cid);

        var branch = await tenant.Branches.FirstOrDefaultAsync(b => b.BranchId == id);
        if (branch == null)
            return NotFound(new { message = $"Branch with ID {id} not found." });

        branch.IsArchived = false;
        branch.IsActive = true;
        branch.ArchivedAt = null;
        branch.ArchivedBy = null;

        await tenant.SaveChangesAsync();

        return Ok(new { message = $"Branch '{branch.BranchName}' successfully restored." });
    }

    // GET: api/tenant/{companyId}/branches/{id}/eligible-users
    [HttpGet("{id:int}/eligible-users")]
    public async Task<IActionResult> GetEligibleUsers(
        int id,
        [FromRoute] int? companyId = null)
    {
        var cid = ResolveCompanyId(companyId);

        // Fetch eligible users belonging to THIS tenant company only
        var users = await _masterDb.Users
            .AsNoTracking()
            .Where(u => u.CompanyId == cid && (u.RoleId == 1 || u.RoleId == 2 || u.RoleId == 3) && u.Status == "Active")
            .OrderBy(u => u.FullName)
            .Select(u => new
            {
                u.UserId,
                u.FullName,
                u.Email,
                u.RoleId,
                RoleName = (u.RoleId == 1 || u.RoleId == 2) ? "Admin" : (u.RoleId == 3 ? "Manager" : "Service Staff"),
                u.BranchId,
                IsAssignedToThisBranch = u.BranchId == id
            })
            .ToListAsync();

        return Ok(users);
    }

    public class AssignAdminRequest
    {
        public int? UserId { get; set; }
    }

    // POST: api/tenant/{companyId}/branches/{id}/assign-admin
    [HttpPost("{id:int}/assign-admin")]
    public async Task<IActionResult> AssignAdmin(
        int id,
        [FromBody] AssignAdminRequest req,
        [FromRoute] int? companyId = null)
    {
        var cid = ResolveCompanyId(companyId);
        await using var tenant = await _tenantFactory.CreateAsync(cid);

        var branch = await tenant.Branches.FirstOrDefaultAsync(b => b.BranchId == id);
        if (branch == null)
            return NotFound(new { message = $"Branch with ID {id} not found." });

        if (branch.IsArchived)
            return BadRequest(new { message = "Cannot assign an administrator to an archived branch." });

        if (req.UserId.HasValue && req.UserId.Value > 0)
        {
            var user = await _masterDb.Users.FirstOrDefaultAsync(u => u.UserId == req.UserId.Value);
            if (user == null)
                return NotFound(new { message = $"User with ID {req.UserId.Value} not found." });

            // CRITICAL TENANT CHECK: Strictly forbid cross-tenant assignment
            if (user.CompanyId != cid)
            {
                return StatusCode(403, new { message = "Security Violation: Cross-tenant branch assignment is strictly forbidden. User belongs to another company." });
            }

            // Unassign other users from this branch if any
            var existingAssigned = await _masterDb.Users
                .Where(u => u.CompanyId == cid && u.BranchId == id && u.UserId != user.UserId)
                .ToListAsync();
            foreach (var eu in existingAssigned)
            {
                eu.BranchId = null;
            }

            user.BranchId = id;
            await _masterDb.SaveChangesAsync();

            return Ok(new
            {
                message = $"Administrator '{user.FullName}' ({user.Email}) successfully assigned to branch '{branch.BranchName}'.",
                branchId = id,
                userId = user.UserId,
                adminName = user.FullName
            });
        }
        else
        {
            // Unassign current branch admin
            var currentlyAssigned = await _masterDb.Users
                .Where(u => u.CompanyId == cid && u.BranchId == id)
                .ToListAsync();
            foreach (var u in currentlyAssigned)
            {
                u.BranchId = null;
            }
            await _masterDb.SaveChangesAsync();

            return Ok(new
            {
                message = $"Branch '{branch.BranchName}' administrator unassigned.",
                branchId = id,
                userId = (int?)null,
                adminName = "Unassigned"
            });
        }
    }

    public class CreateBranchAdminRequest
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public int RoleId { get; set; } = 2; // Default to Admin (2)
        public int? CompanyId { get; set; }
        public int? BranchId { get; set; }
    }

    // POST: api/tenant/{companyId}/branches/{id}/create-admin
    [HttpPost("{id:int}/create-admin")]
    public async Task<IActionResult> CreateBranchAdmin(
        int id,
        [FromBody] CreateBranchAdminRequest req,
        [FromRoute] int? companyId = null)
    {
        var cid = ResolveCompanyId(companyId);
        await using var tenant = await _tenantFactory.CreateAsync(cid);

        int targetBranchId = (req.BranchId.HasValue && req.BranchId.Value > 0) ? req.BranchId.Value : id;
        var branch = await tenant.Branches.FirstOrDefaultAsync(b => b.BranchId == targetBranchId);
        if (branch == null)
            return NotFound(new { message = $"Branch with ID {targetBranchId} not found." });

        if (branch.IsArchived)
            return BadRequest(new { message = "Cannot create an administrator for an archived branch." });

        if (string.IsNullOrWhiteSpace(req.FirstName) || string.IsNullOrWhiteSpace(req.LastName))
            return BadRequest(new { message = "First Name and Last Name are required." });

        if (string.IsNullOrWhiteSpace(req.Email))
            return BadRequest(new { message = "Email is required." });

        if (string.IsNullOrWhiteSpace(req.Password))
            return BadRequest(new { message = "Password is required." });

        var email = req.Email.Trim().ToLowerInvariant();
        var exists = await _masterDb.Users.AnyAsync(u => u.Email.ToLower() == email);
        if (exists)
            return Conflict(new { message = $"A user account with email '{req.Email}' already exists." });

        // Expected Role IDs:
        // 1 = SuperAdmin, 2 = Admin, 3 = Manager, 4 = ServiceStaff
        int assignedRoleId = req.RoleId switch
        {
            2 => 2, // Admin
            3 => 3, // Manager
            4 => 4, // Service Staff
            _ => 2  // Default to Admin (2)
        };

        // Unassign any previous admin for this branch when role is Admin
        if (assignedRoleId == 2)
        {
            var prevAdmins = await _masterDb.Users
                .Where(u => u.CompanyId == cid && u.BranchId == targetBranchId && (u.RoleId == 1 || u.RoleId == 2))
                .ToListAsync();
            foreach (var pa in prevAdmins)
            {
                pa.BranchId = null;
            }
        }

        var newUser = new User
        {
            FirstName = req.FirstName.Trim(),
            LastName = req.LastName.Trim(),
            Email = req.Email.Trim(),
            RoleId = assignedRoleId, // Saved as RoleId = 2 for Admin
            CompanyId = cid,         // Automatically locked to current tenant
            BranchId = targetBranchId, // Automatically locked to this branch
            Status = "Active",
            PasswordHash = req.Password,
            IdentityUserId = string.Empty,
            CreatedAt = DateTime.UtcNow
        };

        _masterDb.Users.Add(newUser);
        await _masterDb.SaveChangesAsync();

        return Ok(new
        {
            userId = newUser.UserId,
            fullName = newUser.FullName,
            email = newUser.Email,
            roleId = newUser.RoleId,
            companyId = newUser.CompanyId,
            branchId = newUser.BranchId,
            message = $"Branch Administrator '{newUser.FullName}' successfully created and assigned to '{branch.BranchName}'."
        });
    }
}
