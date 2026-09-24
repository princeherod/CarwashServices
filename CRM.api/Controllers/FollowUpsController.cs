using CRM.Domain.Entities;
using CRM.domain.Entities;
using CRM.Infrastructure.Data;
using CRM.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CRM.api.Controllers;

[ApiController]
[Route("api/follow-ups")]
public class FollowUpsController : ControllerBase
{
    private readonly MasterErpDbContext _db;
    private readonly ITenantDbContextFactory _tenantFactory;

    public FollowUpsController(MasterErpDbContext db, ITenantDbContextFactory tenantFactory)
    {
        _db = db;
        _tenantFactory = tenantFactory;
    }

    private const int DefaultCompanyId = 1;

    private static readonly string[] AllowedStatuses =
    {
        "Pending", "Scheduled", "Due today", "Sent", "Contacted", "Redeemed", "Expired"
    };

    // ================================================================
    //  AUTO-EXPIRE
    //  Reads run this first so Scheduled rows whose time has arrived
    //  are promoted to Sent automatically.
    // ================================================================
    private async Task<int> AutoExpireAsync()
    {
        var today = DateTime.Today;
        var now = DateTime.Now;

        var toExpire = await _db.FollowUps
            .Where(f => !f.IsArchived
                     && f.Status != "Redeemed"
                     && f.Status != "Expired"
                     && f.ValidUntil.HasValue
                     && f.ValidUntil.Value.Date < today)
            .ToListAsync();

        foreach (var f in toExpire)
            f.Status = "Expired";

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
    //  CREATE  (single)
    //
    //  Trust the ScheduledNow flag from the caller.
    //  Do NOT infer it from the timestamp — Save draft also sends
    //  "now" as the scheduled date but must land as Scheduled.
    // ================================================================
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] FollowUpRequest req)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        await using (var tenant = await _tenantFactory.CreateAsync(DefaultCompanyId))
        {
            bool exists = await tenant.TenantCustomers
                .AnyAsync(c => c.TenantCustomerId == req.CustomerId && !c.IsArchived);

            if (!exists)
                return BadRequest(new { message = $"Customer {req.CustomerId} not found." });
        }

        var scheduled = req.ScheduledDate ?? DateTime.Now;
        bool sendNow = req.ScheduledNow;

        var entity = new FollowUp
        {
            CustomerId = req.CustomerId,
            Type = Clamp(req.Type, 50, "Service Reminder"),
            ContactMethod = Clamp(req.ContactMethod, 50, "SMS"),
            Reason = ClampNullable(req.Reason, 200),
            DiscountOffer = ClampNullable(req.DiscountOffer, 200),
            Notes = ClampNullable(req.Notes, 1000),
            ScheduledDate = scheduled,
            ValidUntil = req.ValidUntil,
            CreatedAt = DateTime.UtcNow,
            IsArchived = false,
            ArchivedAt = null,
            ArchivedBy = null,
            Status = DeriveStatusOnCreate(scheduled, req.ValidUntil, sendNow)
        };

        if (entity.Status == "Sent")
            entity.SentAt = DateTime.Now;

        try
        {
            _db.FollowUps.Add(entity);
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            var inner = ex.InnerException?.Message ?? ex.Message;
            return StatusCode(500, new { message = "Create failed.", detail = inner });
        }

        return CreatedAtAction(nameof(GetById), new { id = entity.FollowUpId }, entity);
    }

    // ================================================================
    //  STATUS DERIVATION
    //
    //  sendNow = true   → Sent      (user clicked Send follow-up + Send now)
    //  sendNow = false  → Scheduled (Save draft OR Schedule — AutoExpire
    //                                later promotes to Sent when time arrives)
    //  ValidUntil past  → Expired   (overrides everything)
    // ================================================================
    private static string DeriveStatusOnCreate(DateTime scheduled, DateTime? validUntil, bool sendNow)
    {
        if (validUntil.HasValue && validUntil.Value.Date < DateTime.Today)
            return "Expired";

        if (sendNow)
            return "Sent";

        return "Scheduled";
    }

    // ================================================================
    //  BULK CREATE
    //
    //  The dialog sends ScheduledNow = true ONLY when the user clicked
    //  Send follow-up + Send now. Save draft and Schedule both send
    //  false, so those rows land as Scheduled.
    // ================================================================
    [HttpPost("bulk")]
    public async Task<IActionResult> BulkCreate([FromBody] BulkFollowUpRequest req)
    {
        if (req.CustomerIds == null || req.CustomerIds.Count == 0)
            return BadRequest(new { message = "At least one customer is required." });

        var requestedIds = req.CustomerIds.Distinct().ToList();

        // Keep only ids that exist in the tenant.
        await using (var tenant = await _tenantFactory.CreateAsync(DefaultCompanyId))
        {
            var existingIds = await tenant.TenantCustomers
                .AsNoTracking()
                .Where(c => requestedIds.Contains(c.TenantCustomerId) && !c.IsArchived)
                .Select(c => c.TenantCustomerId)
                .ToListAsync();

            requestedIds = requestedIds.Intersect(existingIds).ToList();
        }

        if (requestedIds.Count == 0)
            return BadRequest(new { message = "None of the selected customers exist." });

        // A customer who already has an open follow-up cannot receive another.
        var blockedIds = await _db.FollowUps
            .Where(f => requestedIds.Contains(f.CustomerId)
                     && !f.IsArchived
                     && f.Status != "Expired")
            .Select(f => f.CustomerId)
            .Distinct()
            .ToListAsync();

        var allowedIds = requestedIds.Except(blockedIds).ToList();
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

        var type = Clamp(req.Type, 50, "Service Reminder");
        var contact = Clamp(req.ContactMethod, 50, "SMS");
        var reason = ClampNullable(req.Reason, 200);
        var offer = ClampNullable(req.DiscountOffer, 200);
        var notes = ClampNullable(req.Notes, 1000);

        var scheduled = req.ScheduledDate ?? DateTime.Now;
        bool sendNow = req.ScheduledNow;

        var status = DeriveStatusOnCreate(scheduled, req.ValidUntil, sendNow);
        if (!AllowedStatuses.Contains(status)) status = "Scheduled";

        var created = new List<FollowUp>(allowedIds.Count);

        foreach (var cid in allowedIds)
        {
            var f = new FollowUp
            {
                CustomerId = cid,
                Type = type,
                ContactMethod = contact,
                Reason = reason,
                DiscountOffer = offer,
                Notes = notes,
                ScheduledDate = scheduled,
                ValidUntil = req.ValidUntil,
                Status = status,
                SentAt = status == "Sent" ? DateTime.Now : null,
                CreatedAt = DateTime.UtcNow,
                IsArchived = false,
                ArchivedAt = null,
                ArchivedBy = null
            };
            _db.FollowUps.Add(f);
            created.Add(f);
        }

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            var inner = ex.InnerException?.Message ?? ex.Message;
            return StatusCode(500, new { message = "Bulk create failed.", detail = inner });
        }

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
    //  UPDATE
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

        if (req.CustomerId.HasValue && req.CustomerId.Value != existing.CustomerId)
        {
            await using var tenant = await _tenantFactory.CreateAsync(DefaultCompanyId);
            bool exists = await tenant.TenantCustomers
                .AnyAsync(c => c.TenantCustomerId == req.CustomerId.Value && !c.IsArchived);
            if (!exists)
                return BadRequest(new { message = $"Customer {req.CustomerId} not found." });

            existing.CustomerId = req.CustomerId.Value;
        }

        if (!string.IsNullOrWhiteSpace(req.Type)) existing.Type = Clamp(req.Type, 50, existing.Type);
        if (!string.IsNullOrWhiteSpace(req.ContactMethod)) existing.ContactMethod = Clamp(req.ContactMethod, 50, existing.ContactMethod);
        if (req.Reason != null) existing.Reason = ClampNullable(req.Reason, 200);
        if (req.DiscountOffer != null) existing.DiscountOffer = ClampNullable(req.DiscountOffer, 200);
        if (req.Notes != null) existing.Notes = ClampNullable(req.Notes, 1000);
        if (req.ScheduledDate.HasValue) existing.ScheduledDate = req.ScheduledDate.Value;
        if (req.ValidUntil.HasValue) existing.ValidUntil = req.ValidUntil.Value;

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

            if (!AllowedStatuses.Contains(existing.Status))
                existing.Status = "Scheduled";
        }

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            var inner = ex.InnerException?.Message ?? ex.Message;
            return StatusCode(500, new { message = "Update failed.", detail = inner });
        }

        return Ok(existing);
    }

    // ================================================================
    //  REDEEM / MARK-SENT
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

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            var inner = ex.InnerException?.Message ?? ex.Message;
            return StatusCode(500, new { message = "Redeem failed.", detail = inner });
        }

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

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            var inner = ex.InnerException?.Message ?? ex.Message;
            return StatusCode(500, new { message = "Mark-sent failed.", detail = inner });
        }

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

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            var inner = ex.InnerException?.Message ?? ex.Message;
            return StatusCode(500, new { message = "Archive failed.", detail = inner });
        }

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

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            var inner = ex.InnerException?.Message ?? ex.Message;
            return StatusCode(500, new { message = "Restore failed.", detail = inner });
        }

        return Ok(existing);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var row = await _db.FollowUps.FirstOrDefaultAsync(f => f.FollowUpId == id);
        if (row is null)
            return NotFound(new { message = $"FollowUp {id} not found." });

        _db.FollowUps.Remove(row);

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

    // ================================================================
    //  STRING CLAMP HELPERS
    // ================================================================
    private static string Clamp(string? value, int max, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value)) return fallback;
        var trimmed = value.Trim();
        return trimmed.Length > max ? trimmed.Substring(0, max) : trimmed;
    }

    private static string? ClampNullable(string? value, int max)
    {
        if (value == null) return null;
        var trimmed = value.Trim();
        if (trimmed.Length == 0) return null;
        return trimmed.Length > max ? trimmed.Substring(0, max) : trimmed;
    }
}

// ================================================================
//  REQUEST DTOS
// ================================================================

/// <summary>
/// Body for POST api/follow-ups (single create).
/// ScheduledNow carries the user's intent:
///   true  → Send follow-up + Send now  → status Sent
///   false → Schedule or Save draft      → status Scheduled
/// </summary>
public class FollowUpRequest
{
    public int CustomerId { get; set; }
    public string Type { get; set; } = "Service Reminder";
    public string ContactMethod { get; set; } = "SMS";
    public string? Reason { get; set; }
    public string? DiscountOffer { get; set; }
    public string? Notes { get; set; }
    public DateTime? ScheduledDate { get; set; }
    public DateTime? ValidUntil { get; set; }
    public bool ScheduledNow { get; set; }
}

/// <summary>
/// Body for POST api/follow-ups/bulk.
/// Same ScheduledNow semantics as the single create.
/// </summary>
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