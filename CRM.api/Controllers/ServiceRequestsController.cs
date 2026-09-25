using CRM.domain.Entities;
using CRM.Domain.Entities;
using CRM.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CRM.api.Controllers;

[ApiController]
[Route("api/service-requests")]
public class ServiceRequestsController : ControllerBase
{
    private readonly MasterErpDbContext _db;

    public ServiceRequestsController(MasterErpDbContext db)
    {
        _db = db;
    }

    // GET: api/service-requests          → active only
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var list = await _db.ServiceRequests
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
    public async Task<IActionResult> GetArchived()
    {
        var list = await _db.ServiceRequests
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
    public async Task<IActionResult> GetById(int requestId)
    {
        var row = await _db.ServiceRequests
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.RequestId == requestId);

        if (row is null)
            return NotFound(new { message = $"ServiceRequest {requestId} not found." });

        return Ok(row);
    }

    // POST: api/service-requests
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] ServiceRequest req)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        // ---- Backend date validation ----
        if (req.RequestedDate.Date < DateTime.Today)
            return BadRequest(new { message = "Requested date cannot be in the past." });

        if (req.ScheduledDate.HasValue && req.ScheduledDate.Value < req.RequestedDate)
            return BadRequest(new { message = "Scheduled date must be on or after the requested date." });

        // Create is always Pending, and CompletedDate is always null on create.
        req.Status = "Pending";
        req.CompletedDate = null;
        if (req.RequestedDate == default) req.RequestedDate = DateTime.Now;

        req.AssignedStaffId = null;
        req.IsArchived = false;
        req.ArchivedAt = null;
        req.ArchivedBy = null;

        req.CreatedBy = await _db.Users
            .OrderBy(u => u.UserId)
            .Select(u => u.UserId)
            .FirstOrDefaultAsync();

        if (req.CreatedBy == 0)
            return BadRequest(new { message = "No users exist in the database. Seed a user first." });

        _db.ServiceRequests.Add(req);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { requestId = req.RequestId }, req);
    }

    // ================================================================
    //  UPDATE — accepts the full editable surface of a request,
    //  including assignment (AssignedStaffId or ClearAssignment).
    // ================================================================
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

        // Assignment fields.
        //  - AssignedStaffId != null  → assign to that Service Staff user.
        //  - ClearAssignment == true  → unassign.
        //  - Both null/false          → leave existing value untouched.
        public int? AssignedStaffId { get; set; }
        public bool ClearAssignment { get; set; }
    }

    [HttpPut("{requestId:int}")]
    public async Task<IActionResult> Update(int requestId, [FromBody] UpdateRequest req)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var existing = await _db.ServiceRequests
            .FirstOrDefaultAsync(r => r.RequestId == requestId);

        if (existing is null)
            return NotFound(new { message = $"ServiceRequest {requestId} not found." });

        // ---- Backend date validation ----
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

        // ---- Assignment validation ----
        if (req.ClearAssignment)
        {
            existing.AssignedStaffId = null;
        }
        else if (req.AssignedStaffId.HasValue)
        {
            var staffUser = await _db.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.UserId == req.AssignedStaffId.Value);

            if (staffUser == null)
                return BadRequest(new { message = $"User {req.AssignedStaffId} not found." });

            if (staffUser.RoleId != 4)
                return BadRequest(new { message = "Assigned staff must be an existing Service Staff user." });

            existing.AssignedStaffId = req.AssignedStaffId.Value;
        }

        // Editable fields
        existing.CustomerId = req.CustomerId;
        existing.ServiceId = req.ServiceId;
        existing.Priority = req.Priority;
        existing.RequestedDate = req.RequestedDate;
        existing.ScheduledDate = req.ScheduledDate;
        existing.CompletedDate = req.CompletedDate;
        existing.Notes = req.Notes;

        // Status is accepted from the Edit form only.
        if (!string.IsNullOrWhiteSpace(req.Status))
        {
            var allowed = new[] { "Pending", "Assigned", "InProgress", "Completed", "Cancelled" };
            if (!allowed.Contains(req.Status))
                return BadRequest(new { message = $"Status must be one of: {string.Join(", ", allowed)}." });

            existing.Status = req.Status;

            if (req.Status == "Completed" && existing.CompletedDate is null)
                existing.CompletedDate = DateTime.Now;

            if (req.Status != "Completed")
                existing.CompletedDate = null;
        }

        await _db.SaveChangesAsync();
        return Ok(existing);
    }

    // ================================================================
    //  ASSIGN ENDPOINT — used by the Manager's Assign Service Staff view.
    // ================================================================
    public class AssignRequest
    {
        public int? AssignedStaffId { get; set; }
    }

    [HttpPut("{requestId:int}/assign")]
    public async Task<IActionResult> Assign(int requestId, [FromBody] AssignRequest req)
    {
        var existing = await _db.ServiceRequests
            .FirstOrDefaultAsync(r => r.RequestId == requestId);

        if (existing is null)
            return NotFound(new { message = $"ServiceRequest {requestId} not found." });

        if (existing.IsArchived)
            return Conflict(new { message = "Cannot assign staff to an archived request." });

        if (!req.AssignedStaffId.HasValue)
        {
            existing.AssignedStaffId = null;
            await _db.SaveChangesAsync();
            return Ok(existing);
        }

        var staffUser = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.UserId == req.AssignedStaffId.Value);

        if (staffUser == null)
            return BadRequest(new { message = $"User {req.AssignedStaffId} not found." });

        if (staffUser.RoleId != 4)
            return BadRequest(new { message = "Assigned staff must be an existing Service Staff user." });

        existing.AssignedStaffId = req.AssignedStaffId.Value;

        if (string.Equals(existing.Status, "Pending", StringComparison.OrdinalIgnoreCase))
            existing.Status = "Assigned";

        await _db.SaveChangesAsync();
        return Ok(existing);
    }

    // ================================================================
    //  STATUS — generic update (used by Admin/Manager edit)
    // ================================================================
    [HttpPut("{requestId:int}/status")]
    public async Task<IActionResult> UpdateStatus(int requestId, [FromBody] StatusUpdateDto dto)
    {
        var allowed = new[] { "Pending", "Assigned", "InProgress", "Completed", "Cancelled" };
        if (string.IsNullOrWhiteSpace(dto.Status) || !allowed.Contains(dto.Status))
            return BadRequest(new { message = $"Status must be one of: {string.Join(", ", allowed)}." });

        var existing = await _db.ServiceRequests
            .FirstOrDefaultAsync(r => r.RequestId == requestId);

        if (existing is null)
            return NotFound(new { message = $"ServiceRequest {requestId} not found." });

        existing.Status = dto.Status;
        if (dto.Status == "Completed" && existing.CompletedDate is null)
            existing.CompletedDate = DateTime.Now;

        await _db.SaveChangesAsync();
        return Ok(existing);
    }

    // ================================================================
    //  SERVICE STAFF — Update Status (scoped, validated, logged)
    //
    //  Only the Service Staff assigned to the request can call this.
    //  Only valid transitions are accepted. Every change writes a
    //  ServiceStatusLog entry. No other fields are touched.
    // ================================================================
    public class StaffStatusUpdateRequest
    {
        public int StaffId { get; set; }
        public string Status { get; set; } = "";
        public string? Notes { get; set; }
    }

    [HttpPut("{requestId:int}/staff-status")]
    public async Task<IActionResult> UpdateStatusByStaff(int requestId,
                                                          [FromBody] StaffStatusUpdateRequest req)
    {
        if (req.StaffId <= 0)
            return BadRequest(new { message = "StaffId is required." });

        var allowed = new[] { "Pending", "Assigned", "InProgress", "Completed", "Cancelled" };
        if (string.IsNullOrWhiteSpace(req.Status) || !allowed.Contains(req.Status))
            return BadRequest(new { message = $"Status must be one of: {string.Join(", ", allowed)}." });

        var existing = await _db.ServiceRequests
            .FirstOrDefaultAsync(r => r.RequestId == requestId);

        if (existing is null)
            return NotFound(new { message = $"ServiceRequest {requestId} not found." });

        if (existing.IsArchived)
            return Conflict(new { message = "Cannot update an archived request." });

        // ---- Scoping: only the assigned Service Staff may update ----
        if (existing.AssignedStaffId != req.StaffId)
            return StatusCode(403, new { message = "This request is not assigned to you." });

        // ---- Transition validation ----
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
            existing.CompletedDate = now;

        if (string.Equals(to, "Pending", StringComparison.OrdinalIgnoreCase)
            || string.Equals(to, "InProgress", StringComparison.OrdinalIgnoreCase)
            || string.Equals(to, "Cancelled", StringComparison.OrdinalIgnoreCase))
            existing.CompletedDate = null;

        // ---- Log ----
        _db.ServiceStatusLogs.Add(new ServiceStatusLog
        {
            RequestId = existing.RequestId,
            Status = to,
            UpdatedBy = req.StaffId,
            UpdatedAt = now,
            Notes = string.IsNullOrWhiteSpace(req.Notes)
                ? $"Status updated to {to}."
                : req.Notes!.Trim()
        });

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
            existing.RequestId,
            status = existing.Status,
            completedDate = existing.CompletedDate,
            updatedAt = now
        });
    }

    /// <summary>
    /// Allowed transitions for the Service Staff workflow.
    ///   Pending / Assigned → InProgress
    ///   InProgress         → Completed
    ///   Any non-terminal   → Cancelled
    ///   Completed / Cancelled are terminal.
    /// </summary>
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

    // PUT: api/service-requests/5/archive
    public class ArchiveRequest { public string? ArchivedBy { get; set; } }

    [HttpPut("{requestId:int}/archive")]
    public async Task<IActionResult> Archive(int requestId, [FromBody] ArchiveRequest? req)
    {
        var existing = await _db.ServiceRequests
            .FirstOrDefaultAsync(r => r.RequestId == requestId);

        if (existing is null)
            return NotFound(new { message = $"ServiceRequest {requestId} not found." });
        if (existing.IsArchived)
            return Conflict(new { message = "This request is already archived." });

        existing.IsArchived = true;
        existing.ArchivedAt = DateTime.UtcNow;
        existing.ArchivedBy = string.IsNullOrWhiteSpace(req?.ArchivedBy) ? "Admin" : req!.ArchivedBy;

        await _db.SaveChangesAsync();
        return Ok(existing);
    }

    // PUT: api/service-requests/5/restore
    [HttpPut("{requestId:int}/restore")]
    public async Task<IActionResult> Restore(int requestId)
    {
        var existing = await _db.ServiceRequests
            .FirstOrDefaultAsync(r => r.RequestId == requestId);

        if (existing is null)
            return NotFound(new { message = $"ServiceRequest {requestId} not found." });
        if (!existing.IsArchived)
            return Conflict(new { message = "This request is not archived." });

        existing.IsArchived = false;
        existing.ArchivedAt = null;
        existing.ArchivedBy = null;

        await _db.SaveChangesAsync();
        return Ok(existing);
    }

    // DELETE: api/service-requests/5
    [HttpDelete("{requestId:int}")]
    public async Task<IActionResult> Delete(int requestId)
    {
        var row = await _db.ServiceRequests
            .FirstOrDefaultAsync(r => r.RequestId == requestId);

        if (row is null)
            return NotFound(new { message = $"ServiceRequest {requestId} not found." });

        _db.ServiceRequests.Remove(row);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}

public class StatusUpdateDto
{
    public string Status { get; set; } = "";
}