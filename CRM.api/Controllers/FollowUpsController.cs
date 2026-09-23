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

    // ================================================================
    //  AUTO-EXPIRE
    //
    //  Called before every read so status always reflects reality
    //  without any manual editing.
    //
    //  Rules:
    //   - Redeemed rows are never touched.
    //   - If ValidUntil has passed and the row isn't Redeemed, it becomes
    //     Expired — regardless of whether it was Sent, Scheduled or Pending.
    //   - If ScheduledDate has passed and the row is still Scheduled, and the
    //     send time has arrived, it becomes Sent.
    // ================================================================
    private async Task<int> AutoExpireAsync()
    {
        var today = DateTime.Today;
        var now = DateTime.Now;

        // 1) Non-redeemed offers whose ValidUntil has passed → Expired
        var toExpire = await _db.FollowUps
            .Where(f => !f.IsArchived
                     && f.Status != "Redeemed"
                     && f.Status != "Expired"
                     && f.ValidUntil.HasValue
                     && f.ValidUntil.Value.Date < today)
            .ToListAsync();

        foreach (var f in toExpire)
            f.Status = "Expired";

        // 2) Scheduled rows whose ScheduledDate has arrived → Sent
        var toSend = await _db.FollowUps
            .Where(f => !f.IsArchived
                     && f.Status == "Scheduled"
                     && f.ScheduledDate <= now)
            .ToListAsync();

        foreach (var f in toSend)
        {
            f.Status = "Sent";
            f.SentAt ??= f.ScheduledDate;
        }

        int changed = toExpire.Count + toSend.Count;
        if (changed > 0) await _db.SaveChangesAsync();
        return changed;
    }

    // ================================================================
    //  LISTS
    // ================================================================
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        await AutoExpireAsync();

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

    // Optional: everything whose ScheduledDate is today and status = Scheduled.
    [HttpGet("due-today")]
    public async Task<IActionResult> GetDueToday()
    {
        await AutoExpireAsync();
        var today = DateTime.Today;

        var list = await _db.FollowUps
            .AsNoTracking()
            .Where(f => !f.IsArchived
                     && f.Status == "Scheduled"
                     && f.ScheduledDate.Date == today)
            .ToListAsync();
        return Ok(list);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        await AutoExpireAsync();
        var row = await _db.FollowUps.AsNoTracking().FirstOrDefaultAsync(f => f.FollowUpId == id);
        if (row is null)
            return NotFound(new { message = $"FollowUp {id} not found." });
        return Ok(row);
    }

    // ================================================================
    //  STATS
    // ================================================================
    [HttpGet("stats")]
    public async Task<IActionResult> GetStats()
    {
        await AutoExpireAsync();
        var today = DateTime.Today;

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
            .CountAsync(f => !f.IsArchived && f.Status == "Expired");

        return Ok(new { dueToday, offersSent, redeemed, expired });
    }

    // ================================================================
    //  CREATE
    // ================================================================
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] FollowUp req)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        req.CreatedAt = DateTime.UtcNow;
        if (req.ScheduledDate == default) req.ScheduledDate = DateTime.Now;

        // Status derivation on create:
        //   - If ValidUntil has already passed → Expired.
        //   - Else if ScheduledDate > now → Scheduled.
        //   - Else → Sent.
        req.Status = DeriveStatusOnCreate(req.ScheduledDate, req.ValidUntil);
        if (req.Status == "Sent") req.SentAt = DateTime.Now;

        req.IsArchived = false;
        req.ArchivedAt = null;
        req.ArchivedBy = null;

        _db.FollowUps.Add(req);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = req.FollowUpId }, req);
    }

    private static string DeriveStatusOnCreate(DateTime scheduled, DateTime? validUntil)
    {
        if (validUntil.HasValue && validUntil.Value.Date < DateTime.Today)
            return "Expired";
        if (scheduled > DateTime.Now)
            return "Scheduled";
        return "Sent";
    }

    [HttpPost("bulk")]
    public async Task<IActionResult> BulkCreate([FromBody] BulkFollowUpRequest req)
    {
        if (req.CustomerIds == null || req.CustomerIds.Count == 0)
            return BadRequest(new { message = "At least one customer is required." });

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
            var scheduled = req.ScheduledDate ?? DateTime.Now;
            var status = DeriveStatusOnCreate(scheduled, req.ValidUntil);

            var f = new FollowUp
            {
                CustomerId = cid,
                Type = req.Type,
                ContactMethod = req.ContactMethod,
                Reason = req.Reason,
                DiscountOffer = req.DiscountOffer,
                Notes = req.Notes,
                ScheduledDate = scheduled,
                ValidUntil = req.ValidUntil,
                Status = status,
                SentAt = status == "Sent" ? DateTime.Now : null,
                CreatedAt = DateTime.UtcNow,
                IsArchived = false
            };
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

    // ================================================================
    //  UPDATE — extended to accept all editable fields.
    //  Status is NOT accepted from the client; it's re-derived from
    //  ScheduledDate + ValidUntil unless the row is already Redeemed.
    // ================================================================
    public class FollowUpUpdateRequest
    {
        public int? CustomerId { get; set; }
        public string? Type { get; set; }
        public string? ContactMethod { get; set; }
        public string? Reason { get; set; }
        public string? DiscountOffer { get; set; }
        public string? Notes { get; set; }
        public DateTime? ScheduledDate { get; set; }
        public DateTime? ValidUntil { get; set; }
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] FollowUpUpdateRequest req)
    {
        var existing = await _db.FollowUps.FirstOrDefaultAsync(f => f.FollowUpId == id);
        if (existing is null)
            return NotFound(new { message = $"FollowUp {id} not found." });

        if (req.CustomerId.HasValue) existing.CustomerId = req.CustomerId.Value;
        if (!string.IsNullOrWhiteSpace(req.Type)) existing.Type = req.Type;
        if (!string.IsNullOrWhiteSpace(req.ContactMethod)) existing.ContactMethod = req.ContactMethod;
        if (req.Reason != null) existing.Reason = req.Reason;
        if (req.DiscountOffer != null) existing.DiscountOffer = req.DiscountOffer;
        if (req.Notes != null) existing.Notes = req.Notes;
        if (req.ScheduledDate.HasValue) existing.ScheduledDate = req.ScheduledDate.Value;
        if (req.ValidUntil.HasValue) existing.ValidUntil = req.ValidUntil.Value;

        // Never override a Redeemed status. Otherwise, re-derive from the dates.
        if (!string.Equals(existing.Status, "Redeemed", StringComparison.OrdinalIgnoreCase))
        {
            if (existing.ValidUntil.HasValue && existing.ValidUntil.Value.Date < DateTime.Today)
                existing.Status = "Expired";
            else if (existing.ScheduledDate > DateTime.Now)
                existing.Status = "Scheduled";
            else
            {
                if (existing.Status != "Sent") existing.SentAt ??= DateTime.Now;
                existing.Status = "Sent";
            }
        }

        await _db.SaveChangesAsync();
        return Ok(existing);
    }

    // ================================================================
    //  REDEEM / MARK-SENT  (called by the redemption or send flow)
    // ================================================================
    [HttpPost("{id:int}/redeem")]
    public async Task<IActionResult> Redeem(int id)
    {
        var existing = await _db.FollowUps.FirstOrDefaultAsync(f => f.FollowUpId == id);
        if (existing is null)
            return NotFound(new { message = $"FollowUp {id} not found." });

        if (existing.ValidUntil.HasValue && existing.ValidUntil.Value.Date < DateTime.Today)
            return Conflict(new { message = "This offer has already expired and cannot be redeemed." });

        existing.Status = "Redeemed";
        await _db.SaveChangesAsync();
        return Ok(existing);
    }

    [HttpPost("{id:int}/mark-sent")]
    public async Task<IActionResult> MarkSent(int id)
    {
        var existing = await _db.FollowUps.FirstOrDefaultAsync(f => f.FollowUpId == id);
        if (existing is null)
            return NotFound(new { message = $"FollowUp {id} not found." });

        if (string.Equals(existing.Status, "Redeemed", StringComparison.OrdinalIgnoreCase))
            return Conflict(new { message = "This follow-up has already been redeemed." });

        existing.Status = "Sent";
        existing.SentAt ??= DateTime.Now;
        await _db.SaveChangesAsync();
        return Ok(existing);
    }

    // ================================================================
    //  ARCHIVE / RESTORE
    // ================================================================
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