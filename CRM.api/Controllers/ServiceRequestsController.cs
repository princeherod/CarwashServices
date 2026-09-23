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

    // PUT: api/service-requests/5
    [HttpPut("{requestId:int}")]
    public async Task<IActionResult> Update(int requestId, [FromBody] ServiceRequest req)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var existing = await _db.ServiceRequests
            .FirstOrDefaultAsync(r => r.RequestId == requestId);

        if (existing is null)
            return NotFound(new { message = $"ServiceRequest {requestId} not found." });

        // Editable fields
        existing.CustomerId = req.CustomerId;
        existing.ServiceId = req.ServiceId;
        existing.Priority = req.Priority;
        existing.RequestedDate = req.RequestedDate;
        existing.ScheduledDate = req.ScheduledDate;
        existing.CompletedDate = req.CompletedDate;
        existing.Notes = req.Notes;

        // Status / AssignedStaffId / CreatedBy / archive fields are managed by
        // their own dedicated endpoints, NOT by the generic update.
        await _db.SaveChangesAsync();
        return Ok(existing);
    }

    // PUT: api/service-requests/5/status
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
    // Kept for parity but no longer used by the UI. Archive replaces it.
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