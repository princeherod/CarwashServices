using CRM.Domain.Entities;
using CRM.domain.Entities;
using CRM.Infrastructure.Data;
using CRM.Infrastructure.Services;
using CRM.api.Services;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CRM.api.Controllers;

[ApiController]
[Route("api/follow-ups")]
public class FollowUpsController : ControllerBase
{
    private readonly MasterErpDbContext _db;
    private readonly ITenantDbContextFactory _tenantFactory;
    private readonly IEmailSender _emailSender;
    private readonly IOptions<SmtpOptions> _smtpOptions;

    public FollowUpsController(
        MasterErpDbContext db,
        ITenantDbContextFactory tenantFactory,
        IEmailSender emailSender,
        IOptions<SmtpOptions> smtpOptions)
    {
        _db = db;
        _tenantFactory = tenantFactory;
        _emailSender = emailSender;
        _smtpOptions = smtpOptions;
    }

    private const int DefaultCompanyId = 1;

    // "Draft" is a first-class status. A draft is never promoted to Sent by
    // the background pass and never blocks a new follow-up for the customer.
    private static readonly string[] AllowedStatuses =
    {
        "Draft", "Pending", "Scheduled", "Due today", "Sent", "Contacted", "Redeemed", "Expired"
    };

    private string BusinessName
    {
        get
        {
            var name = _smtpOptions.Value.SenderName;
            return string.IsNullOrWhiteSpace(name) ? "AquaShine Car Wash" : name;
        }
    }

    // ================================================================
    //  AUTO-EXPIRE / AUTO-SEND
    //
    //  Runs on every read so the list is always fresh.
    //  • Draft rows are never touched here.
    //  • A Scheduled row past its time is EMAILED first; only successful
    //    sends flip the row to Sent with a SentAt stamp.
    //  • Failed sends stay Scheduled so they retry on the next read.
    //  • Expired rows are expired without an email.
    // ================================================================
    private async Task<int> AutoExpireAsync()
    {
        var today = DateTime.Today;
        var now = DateTime.Now;

        // ---- Expire (no email) ----
        var toExpire = await _db.FollowUps
            .Where(f => !f.IsArchived
                     && f.Status != "Draft"
                     && f.Status != "Redeemed"
                     && f.Status != "Expired"
                     && f.ValidUntil.HasValue
                     && f.ValidUntil.Value.Date < today)
            .ToListAsync();

        foreach (var f in toExpire)
            f.Status = "Expired";

        // ---- Promote Scheduled → Sent WITH an email ----
        var toSend = await _db.FollowUps
            .Where(f => !f.IsArchived
                     && f.Status == "Scheduled"
                     && f.ScheduledDate <= now)
            .ToListAsync();

        int sentCount = 0;
        if (toSend.Count > 0)
        {
            await using var tenant = await _tenantFactory.CreateAsync(DefaultCompanyId);

            foreach (var f in toSend)
            {
                var customer = await tenant.TenantCustomers
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.TenantCustomerId == f.CustomerId);

                if (customer is null || string.IsNullOrWhiteSpace(customer.EmailAddress))
                    continue;   // leave as Scheduled so the user can fix the data

                var subject = FollowUpEmailTemplate.BuildSubject(BusinessName);
                var html = FollowUpEmailTemplate.BuildHtml(
                    customerName: customer.CustomerName ?? "there",
                    messagePreview: f.Notes ?? "",
                    discountOffer: f.DiscountOffer,
                    validUntil: f.ValidUntil,
                    businessName: BusinessName);

                var result = await _emailSender.SendAsync(
                    toEmail: customer.EmailAddress,
                    toName: customer.CustomerName ?? customer.EmailAddress,
                    subject: subject,
                    htmlBody: html);

                if (result.Success)
                {
                    f.Status = "Sent";
                    f.SentAt = DateTime.Now;
                    sentCount++;
                }
                // else: leave as Scheduled — will retry on next read
            }
        }

        int changed = toExpire.Count + sentCount;
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
                          && f.Status != "Draft"
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
    //  CREATE (single)
    // ================================================================
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] FollowUpRequest req)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        if (string.IsNullOrWhiteSpace(req.Reason))
            return BadRequest(new { message = "Reason is required." });

        await using (var tenant = await _tenantFactory.CreateAsync(DefaultCompanyId))
        {
            bool exists = await tenant.TenantCustomers
                .AnyAsync(c => c.TenantCustomerId == req.CustomerId && !c.IsArchived);

            if (!exists)
                return BadRequest(new { message = $"Customer {req.CustomerId} not found." });
        }

        var scheduled = req.ScheduledDate ?? DateTime.Now;

        var entity = new FollowUp
        {
            CustomerId = req.CustomerId,
            Type = Clamp(req.Type, 50, "Service Reminder"),
            ContactMethod = "Email",
            Reason = ClampNullable(req.Reason, 200),
            DiscountOffer = ClampNullable(req.DiscountOffer, 200),
            Notes = ClampNullable(req.Notes, 1000),
            ScheduledDate = scheduled,
            ValidUntil = req.ValidUntil,
            CreatedAt = DateTime.UtcNow,
            IsArchived = false,
            ArchivedAt = null,
            ArchivedBy = null,
            Status = DeriveStatusOnCreate(scheduled, req.ValidUntil, req.ScheduledNow, isDraft: false)
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
    //  isDraft = true   → "Draft"     (no email ever, until user sends)
    //  sendNow = true   → "Sent"      (email immediately)
    //  sendNow = false  → "Scheduled" (email fires when the time arrives)
    //  ValidUntil past  → "Expired"   (overrides everything except Draft)
    // ================================================================
    private static string DeriveStatusOnCreate(
        DateTime scheduled,
        DateTime? validUntil,
        bool sendNow,
        bool isDraft)
    {
        if (isDraft) return "Draft";

        if (validUntil.HasValue && validUntil.Value.Date < DateTime.Today)
            return "Expired";

        if (sendNow) return "Sent";

        return "Scheduled";
    }

    // ================================================================
    //  BULK CREATE
    //
    //  isDraft = true  → every row lands as Draft, no SMTP.
    //  sendNow = true  → email fires per customer, row becomes Sent.
    //  sendNow = false → row becomes Scheduled; the auto-pass sends later.
    // ================================================================
    [HttpPost("bulk")]
    public async Task<IActionResult> BulkCreate([FromBody] BulkFollowUpRequest req)
    {
        if (req.CustomerIds == null || req.CustomerIds.Count == 0)
            return BadRequest(new { message = "At least one customer is required." });

        if (string.IsNullOrWhiteSpace(req.Reason))
            return BadRequest(new { message = "Reason is required." });

        var requestedIds = req.CustomerIds.Distinct().ToList();

        await using var tenant = await _tenantFactory.CreateAsync(DefaultCompanyId);

        var existingIds = await tenant.TenantCustomers
            .AsNoTracking()
            .Where(c => requestedIds.Contains(c.TenantCustomerId) && !c.IsArchived)
            .Select(c => c.TenantCustomerId)
            .ToListAsync();

        requestedIds = requestedIds.Intersect(existingIds).ToList();

        if (requestedIds.Count == 0)
            return BadRequest(new { message = "None of the selected customers exist." });

        // A customer who already has an open follow-up cannot receive another.
        // Draft rows don't block — they can be edited or sent explicitly.
        var blockedIds = await _db.FollowUps
            .Where(f => requestedIds.Contains(f.CustomerId)
                     && !f.IsArchived
                     && f.Status != "Expired"
                     && f.Status != "Draft")
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
        var reason = ClampNullable(req.Reason, 200);
        var offer = ClampNullable(req.DiscountOffer, 200);
        var notes = ClampNullable(req.Notes, 1000);

        var scheduled = req.ScheduledDate ?? DateTime.Now;

        var customers = await tenant.TenantCustomers
            .AsNoTracking()
            .Where(c => allowedIds.Contains(c.TenantCustomerId))
            .ToListAsync();

        var custById = customers.ToDictionary(c => c.TenantCustomerId);

        var created = new List<FollowUp>(allowedIds.Count);
        var failures = new List<object>();
        int sentCount = 0;

        foreach (var cid in allowedIds)
        {
            var status = DeriveStatusOnCreate(
                scheduled,
                req.ValidUntil,
                req.ScheduledNow,
                isDraft: req.IsDraft);

            if (!AllowedStatuses.Contains(status)) status = "Scheduled";

            var entity = new FollowUp
            {
                CustomerId = cid,
                Type = type,
                ContactMethod = "Email",
                Reason = reason,
                DiscountOffer = offer,
                Notes = notes,
                ScheduledDate = scheduled,
                ValidUntil = req.ValidUntil,
                Status = status,
                SentAt = null,
                CreatedAt = DateTime.UtcNow,
                IsArchived = false,
                ArchivedAt = null,
                ArchivedBy = null
            };

            // Only email if the user explicitly chose "Send now".
            if (status == "Sent")
            {
                if (!custById.TryGetValue(cid, out var cust) ||
                    string.IsNullOrWhiteSpace(cust.EmailAddress))
                {
                    failures.Add(new
                    {
                        customerId = cid,
                        error = "No email address on file."
                    });
                    entity.Status = "Scheduled";
                }
                else
                {
                    var subject = FollowUpEmailTemplate.BuildSubject(BusinessName);
                    var html = FollowUpEmailTemplate.BuildHtml(
                        customerName: cust.CustomerName ?? "there",
                        messagePreview: notes ?? "",
                        discountOffer: offer,
                        validUntil: req.ValidUntil,
                        businessName: BusinessName);

                    var result = await _emailSender.SendAsync(
                        toEmail: cust.EmailAddress,
                        toName: cust.CustomerName ?? cust.EmailAddress,
                        subject: subject,
                        htmlBody: html);

                    if (result.Success)
                    {
                        entity.Status = "Sent";
                        entity.SentAt = DateTime.Now;
                        sentCount++;
                    }
                    else
                    {
                        failures.Add(new
                        {
                            customerId = cid,
                            error = result.ErrorMessage ?? "Send failed."
                        });
                        entity.Status = "Scheduled";
                    }
                }
            }

            _db.FollowUps.Add(entity);
            created.Add(entity);
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
            sent = sentCount,
            skipped = blockedIds.Count,
            skippedIds = blockedIds,
            failures,
            message = blockedIds.Count > 0
                ? $"{created.Count} created, {blockedIds.Count} skipped."
                : $"{created.Count} created."
        });
    }

    // ================================================================
    //  UPDATE
    //
    //  Handles the Edit dialog's PUT. Only legal status transition out of
    //  Draft is Draft → Scheduled, and it never triggers SMTP. Non-draft
    //  rows follow the same lifecycle re-derivation as before.
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
        public string? Status { get; set; }
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] FollowUpUpdateRequest req)
    {
        var existing = await _db.FollowUps.FirstOrDefaultAsync(f => f.FollowUpId == id);
        if (existing is null)
            return NotFound(new { message = $"FollowUp {id} not found." });

        // ---- Customer swap (unchanged) ----
        if (req.CustomerId.HasValue && req.CustomerId.Value != existing.CustomerId)
        {
            await using var tenant = await _tenantFactory.CreateAsync(DefaultCompanyId);
            bool exists = await tenant.TenantCustomers
                .AnyAsync(c => c.TenantCustomerId == req.CustomerId.Value && !c.IsArchived);
            if (!exists)
                return BadRequest(new { message = $"Customer {req.CustomerId} not found." });

            existing.CustomerId = req.CustomerId.Value;
        }

        // ---- Field updates ----
        if (!string.IsNullOrWhiteSpace(req.Type))
            existing.Type = Clamp(req.Type, 50, existing.Type);

        existing.ContactMethod = "Email";

        if (req.Reason != null) existing.Reason = ClampNullable(req.Reason, 200);
        if (req.DiscountOffer != null) existing.DiscountOffer = ClampNullable(req.DiscountOffer, 200);
        if (req.Notes != null) existing.Notes = ClampNullable(req.Notes, 1000);
        if (req.ScheduledDate.HasValue) existing.ScheduledDate = req.ScheduledDate.Value;
        if (req.ValidUntil.HasValue) existing.ValidUntil = req.ValidUntil.Value;

        // ============================================================
        //  STATUS
        //
        //  Order matters: apply the explicit status FIRST, then fall back
        //  to the previous value, then run the lifecycle re-derivation only
        //  when the client didn't specify a status at all.
        // ============================================================
        var originalStatus = existing.Status;

        if (!string.IsNullOrWhiteSpace(req.Status))
        {
            var requested = req.Status!.Trim();

            // Legal transitions per current spec:
            //   Draft      → Draft       (stays Draft)
            //   Draft      → Scheduled   (needs a future ScheduledDate)
            //   Anything else keeps its lifecycle-managed status.
            if (string.Equals(originalStatus, "Draft", StringComparison.OrdinalIgnoreCase))
            {
                if (string.Equals(requested, "Draft", StringComparison.OrdinalIgnoreCase))
                {
                    existing.Status = "Draft";
                }
                else if (string.Equals(requested, "Scheduled", StringComparison.OrdinalIgnoreCase))
                {
                    if (existing.ScheduledDate <= DateTime.Now)
                    {
                        return BadRequest(new
                        {
                            message = "Scheduled status requires a ScheduledDate in the future."
                        });
                    }
                    existing.Status = "Scheduled";
                }
                // Any other requested status from Draft is ignored — the row
                // stays Draft. This is the safe default.
            }
            // Non-Draft rows ignore req.Status entirely — their status is
            // driven by lifecycle rules below.
        }

        // ---- Draft short-circuit ----
        // Runs only when the row is still Draft after the explicit-status
        // step. We save and return without touching the lifecycle rules.
        if (string.Equals(existing.Status, "Draft", StringComparison.OrdinalIgnoreCase))
        {
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

        // ---- Explicit transition Draft → Scheduled ----
        // No SMTP fires from Update. Scheduler sends when the time arrives.
        if (!string.IsNullOrWhiteSpace(req.Status)
            && string.Equals(originalStatus, "Draft", StringComparison.OrdinalIgnoreCase)
            && string.Equals(existing.Status, "Scheduled", StringComparison.OrdinalIgnoreCase))
        {
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

        // ---- Lifecycle re-derivation for everything else ----
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

    [HttpPost("{id:int}/redeem")]
    public async Task<IActionResult> Redeem(int id)
    {
        var existing = await _db.FollowUps.FirstOrDefaultAsync(f => f.FollowUpId == id);
        if (existing is null)
            return NotFound(new { message = $"FollowUp {id} not found." });

        if (existing.ValidUntil.HasValue && existing.ValidUntil.Value.Date < DateTime.Today)
            return Conflict(new { message = "This offer has already expired and cannot be redeemed." });

        existing.Status = "Redeemed";

        try { await _db.SaveChangesAsync(); }
        catch (DbUpdateException ex)
        {
            var inner = ex.InnerException?.Message ?? ex.Message;
            return StatusCode(500, new { message = "Redeem failed.", detail = inner });
        }

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

        try { await _db.SaveChangesAsync(); }
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

        try { await _db.SaveChangesAsync(); }
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

        try { await _db.SaveChangesAsync(); }
        catch (DbUpdateException ex)
        {
            var inner = ex.InnerException?.Message ?? ex.Message;
            return StatusCode(500, new { message = "Delete failed.", detail = inner });
        }

        return NoContent();
    }

    // ================================================================
    //  HELPERS
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
//  REQUEST DTOs
// ================================================================
public class FollowUpRequest
{
    public int CustomerId { get; set; }
    public string Type { get; set; } = "Service Reminder";
    public string ContactMethod { get; set; } = "Email";
    public string? Reason { get; set; }
    public string? DiscountOffer { get; set; }
    public string? Notes { get; set; }
    public DateTime? ScheduledDate { get; set; }
    public DateTime? ValidUntil { get; set; }
    public bool ScheduledNow { get; set; }
    public bool IsDraft { get; set; }
}

public class BulkFollowUpRequest
{
    public List<int> CustomerIds { get; set; } = new();
    public string Type { get; set; } = "Service Reminder";
    public string ContactMethod { get; set; } = "Email";
    public string? Reason { get; set; }
    public string? DiscountOffer { get; set; }
    public string? Notes { get; set; }
    public DateTime? ScheduledDate { get; set; }
    public DateTime? ValidUntil { get; set; }
    public bool ScheduledNow { get; set; }
    public bool IsDraft { get; set; }
}