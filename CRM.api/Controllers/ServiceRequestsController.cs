using CRM.domain.Entities;
using CRM.Domain.Entities;
using CRM.Infrastructure.Data;
using CRM.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CRM.api.Controllers;

[ApiController]
[Route("api/service-requests")]
[Route("api/tenant/{companyId}/service-requests")]
public class ServiceRequestsController : ControllerBase
{
    private readonly MasterErpDbContext _masterDb;
    private readonly ITenantDbContextFactory _tenantFactory;

    public ServiceRequestsController(MasterErpDbContext masterDb, ITenantDbContextFactory tenantFactory)
    {
        _masterDb = masterDb;
        _tenantFactory = tenantFactory;
    }

    private int ResolveCompanyId(int? companyId = null)
    {
        if (companyId.HasValue && companyId.Value > 0)
            return companyId.Value;

        if (Request.Query.TryGetValue("companyId", out var qVal) &&
            int.TryParse(qVal.FirstOrDefault(), out var qId) && qId > 0)
            return qId;

        if (RouteData.Values.TryGetValue("companyId", out var val) &&
            int.TryParse(val?.ToString(), out var rId) && rId > 0)
            return rId;

        if (Request.Headers.TryGetValue("X-Company-Id", out var hVal) &&
            int.TryParse(hVal.FirstOrDefault(), out var hId) && hId > 0)
            return hId;

        return 1;
    }

    // GET: api/service-requests          → active only
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int? companyId = null)
    {
        var cid = ResolveCompanyId(companyId);
        await using var tenant = await _tenantFactory.CreateAsync(cid);

        var list = await tenant.ServiceRequests
            .AsNoTracking()
            .Where(r => !r.IsArchived)
            .OrderByDescending(r => r.RequestId)
            .Select(r => new
            {
                r.RequestId,
                r.CustomerId,
                r.ServiceId,
                r.AssignedStaffId,
                r.CreatedBy,
                r.Status,
                r.Priority,
                r.RequestedDate,
                r.ScheduledDate,
                r.CompletedDate,
                r.Notes,
                r.IsArchived,
                r.ArchivedAt,
                r.ArchivedBy
            })
            .ToListAsync();

        return Ok(list);
    }

    // GET: api/service-requests/archived  → archived only
    [HttpGet("archived")]
    public async Task<IActionResult> GetArchived([FromQuery] int? companyId = null)
    {
        var cid = ResolveCompanyId(companyId);
        await using var tenant = await _tenantFactory.CreateAsync(cid);

        var list = await tenant.ServiceRequests
            .AsNoTracking()
            .Where(r => r.IsArchived)
            .OrderByDescending(r => r.ArchivedAt)
            .Select(r => new
            {
                r.RequestId,
                r.CustomerId,
                r.ServiceId,
                r.AssignedStaffId,
                r.CreatedBy,
                r.Status,
                r.Priority,
                r.RequestedDate,
                r.ScheduledDate,
                r.CompletedDate,
                r.Notes,
                r.IsArchived,
                r.ArchivedAt,
                r.ArchivedBy
            })
            .ToListAsync();

        return Ok(list);
    }

    // GET: api/service-requests/5
    [HttpGet("{requestId:int}")]
    public async Task<IActionResult> GetById(int requestId, [FromQuery] int? companyId = null)
    {
        var cid = ResolveCompanyId(companyId);
        await using var tenant = await _tenantFactory.CreateAsync(cid);

        var row = await tenant.ServiceRequests
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.RequestId == requestId);

        if (row is null)
            return NotFound(new { message = $"ServiceRequest {requestId} not found in tenant database." });

        return Ok(row);
    }

    // POST: api/service-requests
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] ServiceRequest req, [FromQuery] int? companyId = null)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var cid = ResolveCompanyId(companyId);
        await using var tenant = await _tenantFactory.CreateAsync(cid);

        // Default requested date if not specified
        if (req.RequestedDate == default) req.RequestedDate = DateTime.Now;

        // ---- Backend date validation ----
        if (req.RequestedDate.Date < DateTime.Today)
            return BadRequest(new { message = "Requested date cannot be in the past." });

        if (req.ScheduledDate.HasValue && req.ScheduledDate.Value < req.RequestedDate)
            return BadRequest(new { message = "Scheduled date must be on or after the requested date." });

        req.Status = "Pending";
        req.CompletedDate = null;

        req.AssignedStaffId = null;
        req.IsArchived = false;
        req.ArchivedAt = null;
        req.ArchivedBy = null;

        if (req.CreatedBy <= 0)
        {
            var defaultUser = await _masterDb.Users
                .Where(u => u.CompanyId == cid || u.CompanyId == null)
                .OrderBy(u => u.UserId)
                .Select(u => u.UserId)
                .FirstOrDefaultAsync();

            req.CreatedBy = defaultUser > 0 ? defaultUser : 1;
        }

        tenant.ServiceRequests.Add(req);
        await tenant.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { requestId = req.RequestId, companyId = cid }, req);
    }

    public class UpdateRequest
    {
        public int CustomerId { get; set; }
        public int ServiceId { get; set; }
        public string? Status { get; set; }
        public string? Priority { get; set; }
        public DateTime RequestedDate { get; set; }
        public DateTime? ScheduledDate { get; set; }
        public DateTime? CompletedDate { get; set; }
        public string? Notes { get; set; }

        public int? AssignedStaffId { get; set; }
        public bool ClearAssignment { get; set; }
    }

    [HttpPut("{requestId:int}")]
    public async Task<IActionResult> Update(int requestId, [FromBody] UpdateRequest req, [FromQuery] int? companyId = null)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var cid = ResolveCompanyId(companyId);
        await using var tenant = await _tenantFactory.CreateAsync(cid);

        var existing = await tenant.ServiceRequests
            .FirstOrDefaultAsync(r => r.RequestId == requestId);

        if (existing is null)
            return NotFound(new { message = $"ServiceRequest {requestId} not found in tenant database." });

        if (req.RequestedDate.Date < DateTime.Today)
            return BadRequest(new { message = "Requested date cannot be in the past." });

        if (req.ScheduledDate.HasValue && req.ScheduledDate.Value < req.RequestedDate)
            return BadRequest(new { message = "Scheduled date must be on or after the requested date." });

        if (req.CompletedDate.HasValue && req.CompletedDate.Value < req.RequestedDate)
            return BadRequest(new { message = "Completed date cannot be earlier than the requested date." });

        if (req.CompletedDate.HasValue
            && req.ScheduledDate.HasValue
            && req.CompletedDate.Value < req.ScheduledDate.Value)
            return BadRequest(new { message = "Completed date cannot be earlier than the scheduled date." });

        if (req.CompletedDate.HasValue
            && !string.Equals(req.Status, "Completed", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { message = "A completed date can only be set when the status is Completed." });

        if (req.ClearAssignment)
        {
            existing.AssignedStaffId = null;
        }
        else if (req.AssignedStaffId.HasValue)
        {
            var staffUser = await _masterDb.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.UserId == req.AssignedStaffId.Value);

            if (staffUser == null)
                return BadRequest(new { message = $"User {req.AssignedStaffId} not found." });

            if (staffUser.RoleId != 3 && staffUser.RoleId != 1 && staffUser.RoleId != 2)
                return BadRequest(new { message = "Assigned staff must be a valid staff or admin user." });

            existing.AssignedStaffId = req.AssignedStaffId.Value;
        }

        existing.CustomerId = req.CustomerId;
        existing.ServiceId = req.ServiceId;
        existing.Priority = req.Priority;
        existing.RequestedDate = req.RequestedDate;
        existing.ScheduledDate = req.ScheduledDate;
        existing.CompletedDate = req.CompletedDate;
        existing.Notes = req.Notes;

        var previousStatus = existing.Status;
        if (!string.IsNullOrWhiteSpace(req.Status))
        {
            var allowed = new[] { "Pending", "Assigned", "InProgress", "Completed", "Cancelled" };
            if (!allowed.Contains(req.Status))
                return BadRequest(new { message = $"Status must be one of: {string.Join(", ", allowed)}." });

            existing.Status = req.Status;

            if (req.Status == "Completed")
            {
                if (existing.CompletedDate is null)
                    existing.CompletedDate = DateTime.Now;

                // Ensure billing transaction exists
                var existingTx = await tenant.BillingTransactions.FirstOrDefaultAsync(b => b.RequestId == existing.RequestId);
                if (existingTx == null)
                {
                    var product = await tenant.Products.FirstOrDefaultAsync(p => p.ProductId == existing.ServiceId);
                    decimal price = product?.UnitPrice ?? 0m;
                    tenant.BillingTransactions.Add(new BillingTransaction
                    {
                        RequestId = existing.RequestId,
                        Amount = price,
                        PaymentStatus = "Paid",
                        TransactionDate = existing.CompletedDate ?? DateTime.Now
                    });
                }
            }

            if (req.Status != "Completed")
                existing.CompletedDate = null;
        }

        if (previousStatus != existing.Status)
        {
            tenant.ServiceStatusLogs.Add(new ServiceStatusLog
            {
                RequestId = existing.RequestId,
                Status = existing.Status,
                UpdatedBy = existing.AssignedStaffId ?? existing.CreatedBy,
                UpdatedAt = DateTime.Now,
                Notes = $"Status updated to {existing.Status}."
            });
        }

        await tenant.SaveChangesAsync();
        return Ok(existing);
    }

    public class AssignRequest
    {
        public int? AssignedStaffId { get; set; }
    }

    [HttpPut("{requestId:int}/assign")]
    public async Task<IActionResult> Assign(int requestId, [FromBody] AssignRequest req, [FromQuery] int? companyId = null)
    {
        var cid = ResolveCompanyId(companyId);
        await using var tenant = await _tenantFactory.CreateAsync(cid);

        var existing = await tenant.ServiceRequests
            .FirstOrDefaultAsync(r => r.RequestId == requestId);

        if (existing is null)
            return NotFound(new { message = $"ServiceRequest {requestId} not found in tenant database." });

        if (existing.IsArchived)
            return Conflict(new { message = "Cannot assign staff to an archived request." });

        if (!req.AssignedStaffId.HasValue)
        {
            existing.AssignedStaffId = null;
            await tenant.SaveChangesAsync();
            return Ok(existing);
        }

        var staffUser = await _masterDb.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.UserId == req.AssignedStaffId.Value);

        if (staffUser == null)
            return BadRequest(new { message = $"User {req.AssignedStaffId} not found." });

        existing.AssignedStaffId = req.AssignedStaffId.Value;

        if (string.Equals(existing.Status, "Pending", StringComparison.OrdinalIgnoreCase))
            existing.Status = "Assigned";

        await tenant.SaveChangesAsync();
        return Ok(existing);
    }

    [HttpPut("{requestId:int}/status")]
    public async Task<IActionResult> UpdateStatus(int requestId, [FromBody] StatusUpdateDto dto, [FromQuery] int? companyId = null)
    {
        var allowed = new[] { "Pending", "Assigned", "InProgress", "Completed", "Cancelled" };
        if (string.IsNullOrWhiteSpace(dto.Status) || !allowed.Contains(dto.Status))
            return BadRequest(new { message = $"Status must be one of: {string.Join(", ", allowed)}." });

        var cid = ResolveCompanyId(companyId);
        await using var tenant = await _tenantFactory.CreateAsync(cid);

        var existing = await tenant.ServiceRequests
            .FirstOrDefaultAsync(r => r.RequestId == requestId);

        if (existing is null)
            return NotFound(new { message = $"ServiceRequest {requestId} not found in tenant database." });

        existing.Status = dto.Status;
        if (string.Equals(dto.Status, "Completed", StringComparison.OrdinalIgnoreCase))
        {
            existing.CompletedDate = DateTime.Now;
            var existingTx = await tenant.BillingTransactions.FirstOrDefaultAsync(b => b.RequestId == existing.RequestId);
            if (existingTx == null)
            {
                var product = await tenant.Products.FirstOrDefaultAsync(p => p.ProductId == existing.ServiceId);
                decimal price = product?.UnitPrice ?? 0m;
                tenant.BillingTransactions.Add(new BillingTransaction
                {
                    RequestId = existing.RequestId,
                    Amount = price,
                    PaymentStatus = "Paid",
                    TransactionDate = DateTime.Now
                });
            }
        }
        else
        {
            existing.CompletedDate = null;
        }

        tenant.ServiceStatusLogs.Add(new ServiceStatusLog
        {
            RequestId = existing.RequestId,
            Status = existing.Status,
            UpdatedBy = existing.AssignedStaffId ?? existing.CreatedBy,
            UpdatedAt = DateTime.Now,
            Notes = $"Status updated to {existing.Status}."
        });

        await tenant.SaveChangesAsync();
        return Ok(existing);
    }

    public class StaffStatusUpdateRequest
    {
        public int StaffId { get; set; }
        public string Status { get; set; } = "";
        public string? Notes { get; set; }
    }

    [HttpPut("{requestId:int}/staff-status")]
    public async Task<IActionResult> UpdateStatusByStaff(int requestId,
                                                          [FromBody] StaffStatusUpdateRequest req,
                                                          [FromQuery] int? companyId = null)
    {
        if (req.StaffId <= 0)
            return BadRequest(new { message = "StaffId is required." });

        var allowed = new[] { "Pending", "Assigned", "InProgress", "Completed", "Cancelled" };
        if (string.IsNullOrWhiteSpace(req.Status) || !allowed.Contains(req.Status))
            return BadRequest(new { message = $"Status must be one of: {string.Join(", ", allowed)}." });

        var cid = ResolveCompanyId(companyId);
        await using var tenant = await _tenantFactory.CreateAsync(cid);

        var existing = await tenant.ServiceRequests
            .FirstOrDefaultAsync(r => r.RequestId == requestId);

        if (existing is null)
            return NotFound(new { message = $"ServiceRequest {requestId} not found in tenant database." });

        if (existing.IsArchived)
            return Conflict(new { message = "Cannot update an archived request." });

        if (existing.AssignedStaffId != req.StaffId)
            return StatusCode(403, new { message = "This request is not assigned to you." });

        var from = (existing.Status ?? "Pending").Trim();
        var to = req.Status.Trim();

        if (!IsValidTransition(from, to))
        {
            return Conflict(new
            {
                message = $"Invalid status transition: {from} → {to}."
            });
        }

        var now = DateTime.Now;
        existing.Status = to;

        if (string.Equals(to, "Completed", StringComparison.OrdinalIgnoreCase))
        {
            existing.CompletedDate = now;
            var existingTx = await tenant.BillingTransactions.FirstOrDefaultAsync(b => b.RequestId == existing.RequestId);
            if (existingTx == null)
            {
                var product = await tenant.Products.FirstOrDefaultAsync(p => p.ProductId == existing.ServiceId);
                decimal price = product?.UnitPrice ?? 0m;
                tenant.BillingTransactions.Add(new BillingTransaction
                {
                    RequestId = existing.RequestId,
                    Amount = price,
                    PaymentStatus = "Paid",
                    TransactionDate = now
                });
            }
        }

        if (string.Equals(to, "Pending", StringComparison.OrdinalIgnoreCase)
            || string.Equals(to, "InProgress", StringComparison.OrdinalIgnoreCase)
            || string.Equals(to, "Cancelled", StringComparison.OrdinalIgnoreCase))
            existing.CompletedDate = null;

        tenant.ServiceStatusLogs.Add(new ServiceStatusLog
        {
            RequestId = existing.RequestId,
            Status = to,
            UpdatedBy = req.StaffId,
            UpdatedAt = now,
            Notes = string.IsNullOrWhiteSpace(req.Notes)
                ? $"Status updated to {to}."
                : req.Notes!.Trim()
        });

        await tenant.SaveChangesAsync();

        return Ok(new
        {
            existing.RequestId,
            status = existing.Status,
            completedDate = existing.CompletedDate,
            updatedAt = now
        });
    }

    private static bool IsValidTransition(string from, string to)
    {
        from = from?.Trim() ?? "Pending";
        to = to?.Trim() ?? "Pending";

        if (string.Equals(from, to, StringComparison.OrdinalIgnoreCase)) return true;

        if (string.Equals(from, "Completed", StringComparison.OrdinalIgnoreCase)) return false;
        if (string.Equals(from, "Cancelled", StringComparison.OrdinalIgnoreCase)) return false;

        if (string.Equals(to, "Cancelled", StringComparison.OrdinalIgnoreCase)) return true;

        if ((string.Equals(from, "Pending", StringComparison.OrdinalIgnoreCase)
             || string.Equals(from, "Assigned", StringComparison.OrdinalIgnoreCase))
            && string.Equals(to, "InProgress", StringComparison.OrdinalIgnoreCase))
            return true;

        if (string.Equals(from, "InProgress", StringComparison.OrdinalIgnoreCase)
            && string.Equals(to, "Completed", StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    public class ArchiveRequest { public string? ArchivedBy { get; set; } }

    [HttpPut("{requestId:int}/archive")]
    public async Task<IActionResult> Archive(int requestId, [FromBody] ArchiveRequest? req, [FromQuery] int? companyId = null)
    {
        var cid = ResolveCompanyId(companyId);
        await using var tenant = await _tenantFactory.CreateAsync(cid);

        var existing = await tenant.ServiceRequests
            .FirstOrDefaultAsync(r => r.RequestId == requestId);

        if (existing is null)
            return NotFound(new { message = $"ServiceRequest {requestId} not found in tenant database." });
        if (existing.IsArchived)
            return Conflict(new { message = "This request is already archived." });

        existing.IsArchived = true;
        existing.ArchivedAt = DateTime.UtcNow;
        existing.ArchivedBy = string.IsNullOrWhiteSpace(req?.ArchivedBy) ? "Admin" : req!.ArchivedBy;

        await tenant.SaveChangesAsync();
        return Ok(existing);
    }

    [HttpPut("{requestId:int}/restore")]
    public async Task<IActionResult> Restore(int requestId, [FromQuery] int? companyId = null)
    {
        var cid = ResolveCompanyId(companyId);
        await using var tenant = await _tenantFactory.CreateAsync(cid);

        var existing = await tenant.ServiceRequests
            .FirstOrDefaultAsync(r => r.RequestId == requestId);

        if (existing is null)
            return NotFound(new { message = $"ServiceRequest {requestId} not found in tenant database." });
        if (!existing.IsArchived)
            return Conflict(new { message = "This request is not archived." });

        existing.IsArchived = false;
        existing.ArchivedAt = null;
        existing.ArchivedBy = null;

        await tenant.SaveChangesAsync();
        return Ok(existing);
    }

    [HttpDelete("{requestId:int}")]
    public async Task<IActionResult> Delete(int requestId, [FromQuery] int? companyId = null)
    {
        var cid = ResolveCompanyId(companyId);
        await using var tenant = await _tenantFactory.CreateAsync(cid);

        var row = await tenant.ServiceRequests
            .FirstOrDefaultAsync(r => r.RequestId == requestId);

        if (row is null)
            return NotFound(new { message = $"ServiceRequest {requestId} not found in tenant database." });

        tenant.ServiceRequests.Remove(row);
        await tenant.SaveChangesAsync();
        return NoContent();
    }
}

public class StatusUpdateDto
{
    public string Status { get; set; } = "";
}