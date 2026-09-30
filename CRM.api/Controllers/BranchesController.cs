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
            customersCount = customerCounts.TryGetValue(b.BranchId, out var cc) ? cc : 0
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
            customersCount = custCount
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
}
