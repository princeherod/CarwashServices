using CRM.Domain.Entities;
using CRM.domain.Entities;
using CRM.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CRM.api.Controllers;

[ApiController]
[Route("api/follow-ups")]
public class FollowUpsController : ControllerBase
{
    private readonly MasterErpDbContext _db;

    public FollowUpsController(MasterErpDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var list = await _db.FollowUps
            .AsNoTracking()
            .Where(f => !f.IsArchived)
            .OrderByDescending(f => f.FollowUpId)
            .ToListAsync();
        return Ok(list);
    }

    [HttpGet("archived")]
    public async Task<IActionResult> GetArchived()
    {
        var list = await _db.FollowUps
            .AsNoTracking()
            .Where(f => f.IsArchived)
            .OrderByDescending(f => f.ArchivedAt)
            .ToListAsync();
        return Ok(list);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var row = await _db.FollowUps.AsNoTracking().FirstOrDefaultAsync(f => f.FollowUpId == id);
        if (row is null)
            return NotFound(new { message = $"FollowUp {id} not found." });
        return Ok(row);
    }

    [HttpGet("stats")]
    public async Task<IActionResult> GetStats()
    {
        var today = DateTime.Today;

        // Stats only count active (non-archived) follow-ups.
        var dueToday = await _db.FollowUps
            .CountAsync(f => !f.IsArchived
                          && f.ScheduledDate.Date == today
                          && f.Status != "Sent"
                          && f.Status != "Redeemed"
                          && f.Status != "Expired");

        var offersSent = await _db.FollowUps
            .CountAsync(f => !f.IsArchived && (f.Status == "Sent" || f.Status == "Contacted"));

        var redeemed = await _db.FollowUps
            .CountAsync(f => !f.IsArchived && f.Status == "Redeemed");

        var expired = await _db.FollowUps
            .CountAsync(f => !f.IsArchived
                          && (f.Status == "Expired"
                              || (f.ValidUntil.HasValue && f.ValidUntil.Value.Date < today && f.Status != "Redeemed")));

        return Ok(new { dueToday, offersSent, redeemed, expired });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] FollowUp req)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        req.CreatedAt = DateTime.UtcNow;
        if (req.ScheduledDate == default) req.ScheduledDate = DateTime.Now;

        if (req.ValidUntil.HasValue && req.ValidUntil.Value.Date < DateTime.Today && req.Status != "Redeemed")
            req.Status = "Expired";

        req.IsArchived = false;
        req.ArchivedAt = null;
        req.ArchivedBy = null;

        _db.FollowUps.Add(req);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = req.FollowUpId }, req);
    }

    [HttpPost("bulk")]
    public async Task<IActionResult> BulkCreate([FromBody] BulkFollowUpRequest req)
    {
        if (req.CustomerIds == null || req.CustomerIds.Count == 0)
            return BadRequest(new { message = "At least one customer is required." });

        var today = DateTime.Today;

        var blockedIds = await _db.FollowUps
            .Where(f => req.CustomerIds.Contains(f.CustomerId)
                     && !f.IsArchived
                     && f.Status != "Expired")
            .Select(f => f.CustomerId)
            .Distinct()
            .ToListAsync();

        var allowedIds = req.CustomerIds.Except(blockedIds).ToList();
        if (allowedIds.Count == 0)
        {
            return Conflict(new
            {
                message = "All selected customers already have an open follow-up.",
                blockedIds,
                skipped = blockedIds.Count,
                count = 0
            });
        }

        var created = new List<FollowUp>();

        foreach (var cid in allowedIds)
        {
            var f = new FollowUp
            {
                CustomerId = cid,
                Type = req.Type,
                ContactMethod = req.ContactMethod,
                Reason = req.Reason,
                DiscountOffer = req.DiscountOffer,
                Notes = req.Notes,
                ScheduledDate = req.ScheduledDate ?? DateTime.Now,
                ValidUntil = req.ValidUntil,
                Status = req.ScheduledNow ? "Sent" : "Scheduled",
                SentAt = req.ScheduledNow ? DateTime.Now : null,
                CreatedAt = DateTime.UtcNow,
                IsArchived = false,
                ArchivedAt = null,
                ArchivedBy = null
            };

            if (f.ValidUntil.HasValue && f.ValidUntil.Value.Date < today)
                f.Status = "Expired";

            _db.FollowUps.Add(f);
            created.Add(f);
        }

        await _db.SaveChangesAsync();

        return Ok(new
        {
            count = created.Count,
            skipped = blockedIds.Count,
            skippedIds = blockedIds,
            message = blockedIds.Count > 0
                ? $"{created.Count} created, {blockedIds.Count} skipped."
                : $"{created.Count} created."
        });
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] FollowUp req)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var existing = await _db.FollowUps.FirstOrDefaultAsync(f => f.FollowUpId == id);
        if (existing is null)
            return NotFound(new { message = $"FollowUp {id} not found." });

        existing.CustomerId = req.CustomerId;
        existing.Type = req.Type;
        existing.ContactMethod = req.ContactMethod;
        existing.Reason = req.Reason;
        existing.DiscountOffer = req.DiscountOffer;
        existing.Notes = req.Notes;
        existing.ScheduledDate = req.ScheduledDate;
        existing.ValidUntil = req.ValidUntil;

        if (!string.IsNullOrWhiteSpace(req.Status))
            existing.Status = req.Status;

        await _db.SaveChangesAsync();
        return Ok(existing);
    }

    public class ArchiveRequest { public string? ArchivedBy { get; set; } }

    [HttpPut("{id:int}/archive")]
    public async Task<IActionResult> Archive(int id, [FromBody] ArchiveRequest? req)
    {
        var existing = await _db.FollowUps.FirstOrDefaultAsync(f => f.FollowUpId == id);
        if (existing is null)
            return NotFound(new { message = $"FollowUp {id} not found." });
        if (existing.IsArchived)
            return Conflict(new { message = "This follow-up is already archived." });

        existing.IsArchived = true;
        existing.ArchivedAt = DateTime.UtcNow;
        existing.ArchivedBy = string.IsNullOrWhiteSpace(req?.ArchivedBy) ? "Admin" : req!.ArchivedBy;

        await _db.SaveChangesAsync();
        return Ok(existing);
    }

    [HttpPut("{id:int}/restore")]
    public async Task<IActionResult> Restore(int id)
    {
        var existing = await _db.FollowUps.FirstOrDefaultAsync(f => f.FollowUpId == id);
        if (existing is null)
            return NotFound(new { message = $"FollowUp {id} not found." });
        if (!existing.IsArchived)
            return Conflict(new { message = "This follow-up is not archived." });

        existing.IsArchived = false;
        existing.ArchivedAt = null;
        existing.ArchivedBy = null;

        await _db.SaveChangesAsync();
        return Ok(existing);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var row = await _db.FollowUps.FirstOrDefaultAsync(f => f.FollowUpId == id);
        if (row is null)
            return NotFound(new { message = $"FollowUp {id} not found." });

        _db.FollowUps.Remove(row);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}

public class BulkFollowUpRequest
{
    public List<int> CustomerIds { get; set; } = new();
    public string Type { get; set; } = "Service Reminder";
    public string ContactMethod { get; set; } = "SMS";
    public string? Reason { get; set; }
    public string? DiscountOffer { get; set; }
    public string? Notes { get; set; }
    public DateTime? ScheduledDate { get; set; }
    public DateTime? ValidUntil { get; set; }
    public bool ScheduledNow { get; set; }
}