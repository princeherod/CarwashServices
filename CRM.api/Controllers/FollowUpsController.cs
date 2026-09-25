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
    private const int RoleServiceStaff = 4;

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
    // ================================================================
    private async Task<int> AutoExpireAsync()
    {
        var today = DateTime.Today;
        var now = DateTime.Now;

        var toExpire = await _db.FollowUps
            .Where(f => !f.IsArchived
                     && f.Status != "Draft"
                     && f.Status != "Redeemed"
                     && f.Status != "Expired"
                     && f.ApprovalStatus != "Pending"
                     && f.ApprovalStatus != "Rejected"
                     && f.ValidUntil.HasValue
                     && f.ValidUntil.Value.Date < today)
            .ToListAsync();

        foreach (var f in toExpire)
            f.Status = "Expired";

        var toSend = await _db.FollowUps
            .Where(f => !f.IsArchived
                     && f.Status == "Scheduled"
                     && f.ApprovalStatus != "Pending"
                     && f.ApprovalStatus != "Rejected"
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
                    continue;

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

    [HttpGet("mine")]
    public async Task<IActionResult> GetMine([FromQuery] int staffId)
    {
        if (staffId <= 0) return BadRequest(new { message = "staffId is required." });

        await AutoExpireAsync();

        var list = await _db.FollowUps
            .AsNoTracking()
            .Where(f => !f.IsArchived && f.CreatedBy == staffId)
            .OrderByDescending(f => f.FollowUpId)
            .ToListAsync();
        return Ok(list);
    }

    [HttpGet("pending-approval")]
    public async Task<IActionResult> GetPendingApproval()
    {
        var list = await _db.FollowUps
            .AsNoTracking()
            .Where(f => !f.IsArchived && f.ApprovalStatus == "Pending")
            .OrderBy(f => f.CreatedAt)
            .ToListAsync();
        return Ok(list);
    }

    [HttpGet("my-customers")]
    public async Task<IActionResult> GetMyCustomers([FromQuery] int staffId,
                                                    [FromQuery] int companyId = 1)
    {
        if (staffId <= 0) return BadRequest(new { message = "staffId is required." });

        var myCustomerIds = await _db.ServiceRequests
            .AsNoTracking()
            .Where(r => !r.IsArchived && r.AssignedStaffId == staffId)
            .Select(r => r.CustomerId)
            .Distinct()
            .ToListAsync();

        if (myCustomerIds.Count == 0) return Ok(Array.Empty<object>());

        await using var tenant = await _tenantFactory.CreateAsync(companyId);

        var customers = await tenant.TenantCustomers
            .AsNoTracking()
            .Where(c => !c.IsArchived && myCustomerIds.Contains(c.TenantCustomerId))
            .OrderBy(c => c.CustomerName)
            .ToListAsync();

        return Ok(customers);
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
                     && f.ApprovalStatus != "Pending"
                     && f.ApprovalStatus != "Rejected"
                     && f.ScheduledDate.Date == today)
            .ToListAsync();
        return Ok(list);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        await AutoExpireAsync();
        var row = await _db.FollowUps.AsNoTracking().FirstOrDefaultAsync(f => f.FollowUpId == id);
        if (row is null) return NotFound(new { message = $"FollowUp {id} not found." });
        return Ok(row);
    }

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
                          && f.Status != "Expired"
                          && f.ApprovalStatus != "Pending"
                          && f.ApprovalStatus != "Rejected");

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
    public async Task<IActionResult> Create([FromBody] FollowUpRequest req)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (string.IsNullOrWhiteSpace(req.Reason))
            return BadRequest(new { message = "Reason is required." });

        int? creatorId = req.CreatedBy;
        bool isServiceStaff = false;
        if (creatorId.HasValue)
        {
            var creator = await _db.Users.AsNoTracking()
                .FirstOrDefaultAsync(u => u.UserId == creatorId.Value);
            if (creator is not null && creator.RoleId == RoleServiceStaff)
                isServiceStaff = true;
        }

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
            CreatedBy = creatorId,
            IsArchived = false,
            ArchivedAt = null,
            ArchivedBy = null,
            ApprovalStatus = isServiceStaff ? "Pending" : "NotRequired",
            Status = isServiceStaff
                ? "Pending"
                : DeriveStatusOnCreate(scheduled, req.ValidUntil, req.ScheduledNow, isDraft: false)
        };

        if (entity.Status == "Sent") entity.SentAt = DateTime.Now;

        try { _db.FollowUps.Add(entity); await _db.SaveChangesAsync(); }
        catch (DbUpdateException ex)
        {
            var inner = ex.InnerException?.Message ?? ex.Message;
            return StatusCode(500, new { message = "Create failed.", detail = inner });
        }

        return CreatedAtAction(nameof(GetById), new { id = entity.FollowUpId }, entity);
    }

    private static string DeriveStatusOnCreate(
        DateTime scheduled, DateTime? validUntil, bool sendNow, bool isDraft)
    {
        if (isDraft) return "Draft";
        if (validUntil.HasValue && validUntil.Value.Date < DateTime.Today) return "Expired";
        if (sendNow) return "Sent";
        return "Scheduled";
    }

    // ================================================================
    //  BULK CREATE
    // ================================================================
    [HttpPost("bulk")]
    public async Task<IActionResult> BulkCreate([FromBody] BulkFollowUpRequest req)
    {
        if (req.CustomerIds == null || req.CustomerIds.Count == 0)
            return BadRequest(new { message = "At least one customer is required." });
        if (string.IsNullOrWhiteSpace(req.Reason))
            return BadRequest(new { message = "Reason is required." });

        int? creatorId = req.CreatedBy;
        bool isServiceStaff = false;
        if (creatorId.HasValue)
        {
            var creator = await _db.Users.AsNoTracking()
                .FirstOrDefaultAsync(u => u.UserId == creatorId.Value);
            if (creator is not null && creator.RoleId == RoleServiceStaff)
                isServiceStaff = true;
        }

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

        var blockedIds = await _db.FollowUps
            .Where(f => requestedIds.Contains(f.CustomerId)
                     && !f.IsArchived
                     && f.Status != "Expired"
                     && f.Status != "Draft"
                     && f.ApprovalStatus != "Rejected")
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
            string status;
            string approval;

            if (isServiceStaff)
            {
                approval = "Pending";
                status = req.IsDraft ? "Draft" : "Pending";
            }
            else
            {
                approval = "NotRequired";
                status = DeriveStatusOnCreate(scheduled, req.ValidUntil, req.ScheduledNow, isDraft: req.IsDraft);
            }

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
                ApprovalStatus = approval,
                SentAt = null,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = creatorId,
                IsArchived = false,
                ArchivedAt = null,
                ArchivedBy = null
            };

            if (!isServiceStaff && status == "Sent")
            {
                if (!custById.TryGetValue(cid, out var cust) ||
                    string.IsNullOrWhiteSpace(cust.EmailAddress))
                {
                    failures.Add(new { customerId = cid, error = "No email address on file." });
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
                        failures.Add(new { customerId = cid, error = result.ErrorMessage ?? "Send failed." });
                        entity.Status = "Scheduled";
                    }
                }
            }

            _db.FollowUps.Add(entity);
            created.Add(entity);
        }

        try { await _db.SaveChangesAsync(); }
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
            pendingApproval = created.Count(f => f.ApprovalStatus == "Pending")
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
        public string? Status { get; set; }
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] FollowUpUpdateRequest req)
    {
        var existing = await _db.FollowUps.FirstOrDefaultAsync(f => f.FollowUpId == id);
        if (existing is null) return NotFound(new { message = $"FollowUp {id} not found." });

        if (existing.ApprovalStatus == "Pending")
        {
            if (!string.IsNullOrWhiteSpace(req.Type)) existing.Type = Clamp(req.Type, 50, existing.Type);
            if (req.Reason != null) existing.Reason = ClampNullable(req.Reason, 200);
            if (req.DiscountOffer != null) existing.DiscountOffer = ClampNullable(req.DiscountOffer, 200);
            if (req.Notes != null) existing.Notes = ClampNullable(req.Notes, 1000);
            if (req.ScheduledDate.HasValue) existing.ScheduledDate = req.ScheduledDate.Value;
            if (req.ValidUntil.HasValue) existing.ValidUntil = req.ValidUntil.Value;

            await _db.SaveChangesAsync();
            return Ok(existing);
        }

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
        existing.ContactMethod = "Email";
        if (req.Reason != null) existing.Reason = ClampNullable(req.Reason, 200);
        if (req.DiscountOffer != null) existing.DiscountOffer = ClampNullable(req.DiscountOffer, 200);
        if (req.Notes != null) existing.Notes = ClampNullable(req.Notes, 1000);
        if (req.ScheduledDate.HasValue) existing.ScheduledDate = req.ScheduledDate.Value;
        if (req.ValidUntil.HasValue) existing.ValidUntil = req.ValidUntil.Value;

        var originalStatus = existing.Status;

        if (!string.IsNullOrWhiteSpace(req.Status))
        {
            var requested = req.Status!.Trim();

            if (string.Equals(originalStatus, "Draft", StringComparison.OrdinalIgnoreCase))
            {
                if (string.Equals(requested, "Draft", StringComparison.OrdinalIgnoreCase))
                    existing.Status = "Draft";
                else if (string.Equals(requested, "Scheduled", StringComparison.OrdinalIgnoreCase))
                {
                    if (existing.ScheduledDate <= DateTime.Now)
                        return BadRequest(new { message = "Scheduled status requires a future date." });
                    existing.Status = "Scheduled";
                }
            }
            else if (string.Equals(originalStatus, "Rejected", StringComparison.OrdinalIgnoreCase))
            {
                if (string.Equals(requested, "Draft", StringComparison.OrdinalIgnoreCase))
                    existing.Status = "Draft";
            }
        }

        if (string.Equals(existing.Status, "Draft", StringComparison.OrdinalIgnoreCase))
        {
            await _db.SaveChangesAsync();
            return Ok(existing);
        }

        if (existing.ApprovalStatus == "NotRequired")
        {
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
        }

        try { await _db.SaveChangesAsync(); }
        catch (DbUpdateException ex)
        {
            var inner = ex.InnerException?.Message ?? ex.Message;
            return StatusCode(500, new { message = "Update failed.", detail = inner });
        }

        return Ok(existing);
    }

    // ================================================================
    //  SEND
    // ================================================================
    public class SendFollowUpRequest { public int? CompanyId { get; set; } }

    [HttpPost("{id:int}/send")]
    public async Task<IActionResult> SendEmail(int id, [FromBody] SendFollowUpRequest? req)
    {
        var existing = await _db.FollowUps.FirstOrDefaultAsync(f => f.FollowUpId == id);
        if (existing is null) return NotFound(new { message = $"FollowUp {id} not found." });

        if (existing.IsArchived)
            return Conflict(new { message = "This follow-up is archived." });
        if (existing.ApprovalStatus == "Pending")
            return Conflict(new { message = "This follow-up is waiting for Admin approval." });
        if (existing.ApprovalStatus == "Rejected")
            return Conflict(new { message = "This follow-up was rejected and cannot be sent." });
        if (string.Equals(existing.Status, "Redeemed", StringComparison.OrdinalIgnoreCase))
            return Conflict(new { message = "Already redeemed." });
        if (string.Equals(existing.Status, "Sent", StringComparison.OrdinalIgnoreCase))
            return Conflict(new { message = "Already sent." });
        if (string.Equals(existing.Status, "Expired", StringComparison.OrdinalIgnoreCase))
            return Conflict(new { message = "Already expired." });

        var companyId = req?.CompanyId ?? DefaultCompanyId;
        await using var tenant = await _tenantFactory.CreateAsync(companyId);

        var customer = await tenant.TenantCustomers
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.TenantCustomerId == existing.CustomerId);

        if (customer is null)
            return BadRequest(new { message = "Customer record not found." });
        if (string.IsNullOrWhiteSpace(customer.EmailAddress))
            return BadRequest(new { message = "Customer has no email on file." });

        var subject = FollowUpEmailTemplate.BuildSubject(BusinessName);
        var html = FollowUpEmailTemplate.BuildHtml(
            customerName: customer.CustomerName ?? "there",
            messagePreview: existing.Notes ?? "",
            discountOffer: existing.DiscountOffer,
            validUntil: existing.ValidUntil,
            businessName: BusinessName);

        var result = await _emailSender.SendAsync(
            toEmail: customer.EmailAddress,
            toName: customer.CustomerName ?? customer.EmailAddress,
            subject: subject,
            htmlBody: html);

        if (!result.Success)
        {
            return StatusCode(500, new
            {
                message = "Unable to send the follow-up email.",
                detail = result.ErrorMessage
            });
        }

        existing.Status = "Sent";
        existing.SentAt = DateTime.Now;
        await _db.SaveChangesAsync();

        return Ok(new
        {
            message = $"Follow-up email sent successfully to {customer.CustomerName}.",
            followUpId = existing.FollowUpId,
            status = existing.Status,
            sentAt = existing.SentAt
        });
    }

    // ================================================================
    //  SUBMIT FOR APPROVAL
    // ================================================================
    public class SubmitApprovalRequest
    {
        public int? SubmittedBy { get; set; }
        public bool SendNow { get; set; }
        public DateTime? ScheduledDate { get; set; }
    }

    [HttpPost("{id:int}/submit-for-approval")]
    public async Task<IActionResult> SubmitForApproval(int id, [FromBody] SubmitApprovalRequest? req)
    {
        var existing = await _db.FollowUps.FirstOrDefaultAsync(f => f.FollowUpId == id);
        if (existing is null) return NotFound(new { message = $"FollowUp {id} not found." });

        if (existing.IsArchived)
            return Conflict(new { message = "Archived follow-ups cannot be submitted." });
        if (existing.ApprovalStatus == "Pending")
            return Conflict(new { message = "Already waiting for approval." });
        if (existing.ApprovalStatus == "Approved")
            return Conflict(new { message = "Already approved." });

        if (!string.Equals(existing.Status, "Draft", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(existing.Status, "Rejected", StringComparison.OrdinalIgnoreCase))
        {
            return Conflict(new
            {
                message = "Only Draft or Rejected follow-ups can be submitted for approval."
            });
        }

        if (req?.ScheduledDate.HasValue == true)
            existing.ScheduledDate = req.ScheduledDate.Value;

        bool sendNow = req?.SendNow ?? false;
        if (!sendNow && existing.ScheduledDate <= DateTime.Now)
            return BadRequest(new { message = "Scheduled time must be in the future." });

        existing.ApprovalStatus = "Pending";
        existing.Status = "Pending";
        existing.RejectionReason = null;
        existing.RejectedBy = null;
        existing.RejectedAt = null;
        existing.ApprovedAt = null;
        existing.ApprovedBy = null;

        await _db.SaveChangesAsync();
        return Ok(existing);
    }

    // ================================================================
    //  APPROVE — Admin OR Manager
    // ================================================================
    public class ApproveRequest
    {
        public int ApprovedBy { get; set; }
        public bool SendNow { get; set; }
    }

    [HttpPost("{id:int}/approve")]
    public async Task<IActionResult> Approve(int id, [FromBody] ApproveRequest req)
    {
        if (req.ApprovedBy <= 0)
            return BadRequest(new { message = "ApprovedBy is required." });

        var approver = await _db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.UserId == req.ApprovedBy);
        if (approver is null) return BadRequest(new { message = "Approver not found." });
        if (approver.RoleId == RoleServiceStaff)
            return Conflict(new { message = "Service Staff cannot approve follow-ups." });

        var existing = await _db.FollowUps.FirstOrDefaultAsync(f => f.FollowUpId == id);
        if (existing is null) return NotFound(new { message = $"FollowUp {id} not found." });

        if (existing.ApprovalStatus != "Pending")
            return Conflict(new { message = "This follow-up is not waiting for approval." });

        existing.ApprovalStatus = "Approved";
        existing.ApprovedBy = req.ApprovedBy;
        existing.ApprovedAt = DateTime.Now;
        existing.RejectionReason = null;
        existing.RejectedBy = null;
        existing.RejectedAt = null;

        if (req.SendNow)
        {
            await using var tenant = await _tenantFactory.CreateAsync(DefaultCompanyId);
            var customer = await tenant.TenantCustomers
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.TenantCustomerId == existing.CustomerId);

            if (customer is null || string.IsNullOrWhiteSpace(customer.EmailAddress))
            {
                existing.Status = "Scheduled";
                await _db.SaveChangesAsync();
                return Ok(new
                {
                    message = "Approved. Customer has no email — follow-up left Scheduled.",
                    followUpId = existing.FollowUpId,
                    status = existing.Status
                });
            }

            var subject = FollowUpEmailTemplate.BuildSubject(BusinessName);
            var html = FollowUpEmailTemplate.BuildHtml(
                customerName: customer.CustomerName ?? "there",
                messagePreview: existing.Notes ?? "",
                discountOffer: existing.DiscountOffer,
                validUntil: existing.ValidUntil,
                businessName: BusinessName);

            var result = await _emailSender.SendAsync(
                toEmail: customer.EmailAddress,
                toName: customer.CustomerName ?? customer.EmailAddress,
                subject: subject,
                htmlBody: html);

            if (result.Success)
            {
                existing.Status = "Sent";
                existing.SentAt = DateTime.Now;
                await _db.SaveChangesAsync();
                return Ok(new
                {
                    message = "Approved and sent.",
                    followUpId = existing.FollowUpId,
                    status = existing.Status,
                    sentAt = existing.SentAt
                });
            }

            existing.Status = "Scheduled";
            await _db.SaveChangesAsync();
            return Ok(new
            {
                message = "Approved. Send failed — left Scheduled.",
                followUpId = existing.FollowUpId,
                status = existing.Status,
                detail = result.ErrorMessage
            });
        }
        else
        {
            existing.Status = "Scheduled";
            await _db.SaveChangesAsync();
            return Ok(new
            {
                message = "Approved. Follow-up will be sent when the scheduled time is reached.",
                followUpId = existing.FollowUpId,
                status = existing.Status
            });
        }
    }

    // ================================================================
    //  REJECT — Admin OR Manager. Records RejectedBy and RejectedAt.
    // ================================================================
    public class RejectRequest
    {
        public int RejectedBy { get; set; }
        public string? Reason { get; set; }
    }

    [HttpPost("{id:int}/reject")]
    public async Task<IActionResult> Reject(int id, [FromBody] RejectRequest req)
    {
        if (req.RejectedBy <= 0)
            return BadRequest(new { message = "RejectedBy is required." });

        var rejector = await _db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.UserId == req.RejectedBy);
        if (rejector is null) return BadRequest(new { message = "Rejector not found." });
        if (rejector.RoleId == RoleServiceStaff)
            return Conflict(new { message = "Service Staff cannot reject follow-ups." });

        var existing = await _db.FollowUps.FirstOrDefaultAsync(f => f.FollowUpId == id);
        if (existing is null) return NotFound(new { message = $"FollowUp {id} not found." });

        if (existing.ApprovalStatus != "Pending")
            return Conflict(new { message = "This follow-up is not waiting for approval." });

        existing.ApprovalStatus = "Rejected";
        existing.Status = "Rejected";
        existing.RejectionReason = ClampNullable(req.Reason, 500);
        existing.RejectedBy = req.RejectedBy;
        existing.RejectedAt = DateTime.Now;
        existing.ApprovedBy = null;
        existing.ApprovedAt = null;

        await _db.SaveChangesAsync();
        return Ok(existing);
    }

    // ================================================================
    //  MARK-SENT, REDEEM, ARCHIVE, RESTORE, DELETE
    // ================================================================
    [HttpPost("{id:int}/mark-sent")]
    public async Task<IActionResult> MarkSent(int id)
    {
        var existing = await _db.FollowUps.FirstOrDefaultAsync(f => f.FollowUpId == id);
        if (existing is null) return NotFound(new { message = $"FollowUp {id} not found." });

        if (existing.ApprovalStatus == "Pending" || existing.ApprovalStatus == "Rejected")
            return Conflict(new { message = "Cannot mark-sent while awaiting approval or after rejection." });
        if (string.Equals(existing.Status, "Redeemed", StringComparison.OrdinalIgnoreCase))
            return Conflict(new { message = "Already redeemed." });

        existing.Status = "Sent";
        existing.SentAt ??= DateTime.Now;

        try { await _db.SaveChangesAsync(); }
        catch (DbUpdateException ex)
        {
            var inner = ex.InnerException?.Message ?? ex.Message;
            return StatusCode(500, new { message = "Mark-sent failed.", detail = inner });
        }
        return Ok(existing);
    }

    [HttpPost("{id:int}/redeem")]
    public async Task<IActionResult> Redeem(int id)
    {
        var existing = await _db.FollowUps.FirstOrDefaultAsync(f => f.FollowUpId == id);
        if (existing is null) return NotFound(new { message = $"FollowUp {id} not found." });

        if (existing.ValidUntil.HasValue && existing.ValidUntil.Value.Date < DateTime.Today)
            return Conflict(new { message = "This offer has already expired." });

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
        if (existing is null) return NotFound(new { message = $"FollowUp {id} not found." });
        if (existing.IsArchived) return Conflict(new { message = "Already archived." });

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
        if (existing is null) return NotFound(new { message = $"FollowUp {id} not found." });
        if (!existing.IsArchived) return Conflict(new { message = "Not archived." });

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
        if (row is null) return NotFound(new { message = $"FollowUp {id} not found." });

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
    public int? CreatedBy { get; set; }
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
    public int? CreatedBy { get; set; }
}