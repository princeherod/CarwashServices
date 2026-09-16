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

    // GET: api/service-requests
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var list = await _db.ServiceRequests
            .AsNoTracking()
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
                r.Notes
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
    // Status is forced to "Pending". Staff / CreatedBy are set server-side.
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] ServiceRequest req)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        req.Status = "Pending";
        req.CompletedDate = null;
        if (req.RequestedDate == default) req.RequestedDate = DateTime.Now;

        req.AssignedStaffId = null;

        // Pick the first existing user as creator (replace with logged-in user later).
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
    // Only editable fields are updated. Status / AssignedStaffId / CreatedBy
    // are deliberately left alone.
    [HttpPut("{requestId:int}")]
    public async Task<IActionResult> Update(int requestId, [FromBody] ServiceRequest req)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var existing = await _db.ServiceRequests
            .FirstOrDefaultAsync(r => r.RequestId == requestId);

        if (existing is null)
            return NotFound(new { message = $"ServiceRequest {requestId} not found." });

        // ---- Editable fields ----
        existing.CustomerId = req.CustomerId;
        existing.ServiceId = req.ServiceId;
        existing.Priority = req.Priority;
        existing.RequestedDate = req.RequestedDate;
        existing.ScheduledDate = req.ScheduledDate;
        existing.CompletedDate = req.CompletedDate;
        existing.Notes = req.Notes;

        // ---- Not touched ----
        // existing.Status
        // existing.AssignedStaffId
        // existing.CreatedBy

        await _db.SaveChangesAsync();
        return Ok(existing);
    }

    // PUT: api/service-requests/5/status
    // Only Service Staff should call this.
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