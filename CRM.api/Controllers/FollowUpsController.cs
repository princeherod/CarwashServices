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
[Route("api/tenant/{companyId}/follow-ups")]
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

    private const int RoleServiceStaff = 3;

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

    private int ResolveCompanyId(int? companyId = null)
    {
        if (companyId.HasValue && companyId.Value > 0)
            return companyId.Value;

        if (Request.Query.TryGetValue("companyId", out var qVal) &&
            int.TryParse(qVal.FirstOrDefault(), out var qId) && qId > 0)
            return qId;

        if (RouteData.Values.TryGetValue("companyId", out var rVal) &&
            int.TryParse(rVal?.ToString(), out var rId) && rId > 0)
            return rId;

        if (Request.Headers.TryGetValue("X-Company-Id", out var hVal) &&
            int.TryParse(hVal.FirstOrDefault(), out var hId) && hId > 0)
            return hId;

        return 1;
    }

    // ================================================================
    //  AUTO-EXPIRE / AUTO-SEND
    // ================================================================
    private async Task<int> AutoExpireAsync(TenantErpDbContext tenant)
    {
        var today = DateTime.Today;
        var now = DateTime.Now;

        var toExpire = await tenant.FollowUps
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

        var toSend = await tenant.FollowUps
            .Where(f => !f.IsArchived
                     && f.Status == "Scheduled"
                     && f.ApprovalStatus != "Pending"
                     && f.ApprovalStatus != "Rejected"
                     && f.ScheduledDate <= now)
            .ToListAsync();

        int sentCount = 0;
        if (toSend.Count > 0)
        {
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
        if (changed > 0) await tenant.SaveChangesAsync();
        return changed;
    }

    // ================================================================
    //  LISTS
    // ================================================================
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int? companyId = null,
        [FromQuery] int? branchId = null)
    {
        var cid = ResolveCompanyId(companyId);

        var sec = await CRM.api.Services.BranchSecurityHelper.ResolveAndValidateAsync(_db, HttpContext, cid, branchId);
        if (!sec.Allowed)
        {
            return StatusCode(403, new { message = sec.ErrorMessage });
        }
        branchId = sec.EffectiveBranchId;

        await using var tenant = await _tenantFactory.CreateAsync(cid);
        await AutoExpireAsync(tenant);

        var query = tenant.FollowUps
            .AsNoTracking()
            .Where(f => !f.IsArchived);

        if (branchId.HasValue && branchId.Value > 0)
        {
            query = query.Where(f => f.BranchId == branchId.Value);
        }

        var list = await query
            .OrderByDescending(f => f.FollowUpId)
            .ToListAsync();
        return Ok(list);
    }

    [HttpGet("mine")]
    public async Task<IActionResult> GetMine([FromQuery] int staffId, [FromQuery] int? companyId = null, [FromQuery] int? branchId = null)
    {
        if (staffId <= 0) return BadRequest(new { message = "staffId is required." });

        var cid = ResolveCompanyId(companyId);
        var sec = await CRM.api.Services.BranchSecurityHelper.ResolveAndValidateAsync(_db, HttpContext, cid, branchId);
        if (!sec.Allowed) return StatusCode(403, new { message = sec.ErrorMessage });
        branchId = sec.EffectiveBranchId;

        await using var tenant = await _tenantFactory.CreateAsync(cid);
        await AutoExpireAsync(tenant);

        var query = tenant.FollowUps
            .AsNoTracking()
            .Where(f => !f.IsArchived && f.CreatedBy == staffId);

        if (branchId.HasValue && branchId.Value > 0)
        {
            query = query.Where(f => f.BranchId == branchId.Value);
        }

        var list = await query
            .OrderByDescending(f => f.FollowUpId)
            .ToListAsync();
        return Ok(list);
    }

    [HttpGet("pending-approval")]
    public async Task<IActionResult> GetPendingApproval([FromQuery] int? companyId = null, [FromQuery] int? branchId = null)
    {
        var cid = ResolveCompanyId(companyId);
        var sec = await CRM.api.Services.BranchSecurityHelper.ResolveAndValidateAsync(_db, HttpContext, cid, branchId);
        if (!sec.Allowed) return StatusCode(403, new { message = sec.ErrorMessage });
        branchId = sec.EffectiveBranchId;

        await using var tenant = await _tenantFactory.CreateAsync(cid);

        var query = tenant.FollowUps
            .AsNoTracking()
            .Where(f => !f.IsArchived && f.ApprovalStatus == "Pending");

        if (branchId.HasValue && branchId.Value > 0)
        {
            query = query.Where(f => f.BranchId == branchId.Value);
        }

        var list = await query
            .OrderBy(f => f.CreatedAt)
            .ToListAsync();
        return Ok(list);
    }

    [HttpGet("my-customers")]
    public async Task<IActionResult> GetMyCustomers([FromQuery] int staffId,
                                                    [FromQuery] int? companyId = null,
                                                    [FromQuery] int? branchId = null)
    {
        if (staffId <= 0) return BadRequest(new { message = "staffId is required." });

        var cid = ResolveCompanyId(companyId);
        var sec = await CRM.api.Services.BranchSecurityHelper.ResolveAndValidateAsync(_db, HttpContext, cid, branchId);
        if (!sec.Allowed) return StatusCode(403, new { message = sec.ErrorMessage });
        branchId = sec.EffectiveBranchId;

        await using var tenant = await _tenantFactory.CreateAsync(cid);

        var reqQuery = tenant.ServiceRequests
            .AsNoTracking()
            .Where(r => !r.IsArchived && r.AssignedStaffId == staffId);

        if (branchId.HasValue && branchId.Value > 0)
        {
            reqQuery = reqQuery.Where(r => r.BranchId == branchId.Value);
        }

        var myCustomerIds = await reqQuery
            .Select(r => r.CustomerId)
            .Distinct()
            .ToListAsync();

        if (myCustomerIds.Count == 0) return Ok(Array.Empty<object>());

        var custQuery = tenant.TenantCustomers
            .AsNoTracking()
            .Where(c => !c.IsArchived && myCustomerIds.Contains(c.TenantCustomerId));

        if (branchId.HasValue && branchId.Value > 0)
        {
            custQuery = custQuery.Where(c => c.BranchId == branchId.Value);
        }

        var customers = await custQuery
            .OrderBy(c => c.CustomerName)
            .ToListAsync();

        return Ok(customers);
    }

    [HttpGet("customers")]
    public async Task<IActionResult> GetCustomers([FromQuery] int? companyId = null, [FromQuery] int? branchId = null)
    {
        var cid = ResolveCompanyId(companyId);
        var sec = await CRM.api.Services.BranchSecurityHelper.ResolveAndValidateAsync(_db, HttpContext, cid, branchId);
        if (!sec.Allowed) return StatusCode(403, new { message = sec.ErrorMessage });
        branchId = sec.EffectiveBranchId;

        await using var tenant = await _tenantFactory.CreateAsync(cid);

        var query = tenant.TenantCustomers
            .AsNoTracking()
            .Where(c => !c.IsArchived);

        if (branchId.HasValue && branchId.Value > 0)
        {
            query = query.Where(c => c.BranchId == branchId.Value);
        }

        var customers = await query
            .OrderBy(c => c.CustomerName)
            .Select(c => new
            {
                c.TenantCustomerId,
                c.CustomerCode,
                CustomerName = c.CustomerName ?? (c.FirstName + " " + c.LastName).Trim(),
                c.ContactNumber,
                c.EmailAddress,
                c.PlateNumber,
                c.VehicleMake,
                c.VehicleModel,
                c.VehicleColor,
                c.VehicleType,
                c.Source,
                c.BranchId
            })
            .ToListAsync();

        return Ok(customers);
    }

    [HttpGet("archived")]
    public async Task<IActionResult> GetArchived(
        [FromQuery] int? companyId = null,
        [FromQuery] int? branchId = null)
    {
        var cid = ResolveCompanyId(companyId);

        var sec = await CRM.api.Services.BranchSecurityHelper.ResolveAndValidateAsync(_db, HttpContext, cid, branchId);
        if (!sec.Allowed)
        {
            return StatusCode(403, new { message = sec.ErrorMessage });
        }
        branchId = sec.EffectiveBranchId;

        await using var tenant = await _tenantFactory.CreateAsync(cid);

        var query = tenant.FollowUps
            .AsNoTracking()
            .Where(f => f.IsArchived);

        if (branchId.HasValue && branchId.Value > 0)
        {
            query = query.Where(f => f.BranchId == branchId.Value);
        }

        var list = await query
            .OrderByDescending(f => f.ArchivedAt)
            .ToListAsync();
        return Ok(list);
    }

    [HttpGet("due-today")]
    public async Task<IActionResult> GetDueToday([FromQuery] int? companyId = null, [FromQuery] int? branchId = null)
    {
        var cid = ResolveCompanyId(companyId);
        var sec = await CRM.api.Services.BranchSecurityHelper.ResolveAndValidateAsync(_db, HttpContext, cid, branchId);
        if (!sec.Allowed) return StatusCode(403, new { message = sec.ErrorMessage });
        branchId = sec.EffectiveBranchId;

        await using var tenant = await _tenantFactory.CreateAsync(cid);
        await AutoExpireAsync(tenant);
        var today = DateTime.Today;

        var query = tenant.FollowUps
            .AsNoTracking()
            .Where(f => !f.IsArchived
                     && f.Status == "Scheduled"
                     && f.ApprovalStatus != "Pending"
                     && f.ApprovalStatus != "Rejected"
                     && f.ScheduledDate.Date == today);

        if (branchId.HasValue && branchId.Value > 0)
        {
            query = query.Where(f => f.BranchId == branchId.Value);
        }

        var list = await query.ToListAsync();
        return Ok(list);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, [FromQuery] int? companyId = null)
    {
        var cid = ResolveCompanyId(companyId);
        await using var tenant = await _tenantFactory.CreateAsync(cid);
        await AutoExpireAsync(tenant);

        var row = await tenant.FollowUps.AsNoTracking().FirstOrDefaultAsync(f => f.FollowUpId == id);
        if (row is null) return NotFound(new { message = $"FollowUp {id} not found." });
        return Ok(row);
    }

    [HttpGet("stats")]
    public async Task<IActionResult> GetStats([FromQuery] int? companyId = null, [FromQuery] int? branchId = null)
    {
        var cid = ResolveCompanyId(companyId);
        var sec = await CRM.api.Services.BranchSecurityHelper.ResolveAndValidateAsync(_db, HttpContext, cid, branchId);
        if (!sec.Allowed) return StatusCode(403, new { message = sec.ErrorMessage });
        branchId = sec.EffectiveBranchId;

        await using var tenant = await _tenantFactory.CreateAsync(cid);
        await AutoExpireAsync(tenant);
        var today = DateTime.Today;

        var baseQuery = tenant.FollowUps.Where(f => !f.IsArchived);
        if (branchId.HasValue && branchId.Value > 0)
        {
            baseQuery = baseQuery.Where(f => f.BranchId == branchId.Value);
        }

        var dueToday = await baseQuery
            .CountAsync(f => f.ScheduledDate.Date == today
                          && f.Status != "Draft"
                          && f.Status != "Sent"
                          && f.Status != "Redeemed"
                          && f.Status != "Expired"
                          && f.ApprovalStatus != "Pending"
                          && f.ApprovalStatus != "Rejected");

        var offersSent = await baseQuery
            .CountAsync(f => f.Status == "Sent" || f.Status == "Contacted");
        var redeemed = await baseQuery
            .CountAsync(f => f.Status == "Redeemed");
        var expired = await baseQuery
            .CountAsync(f => f.Status == "Expired");

        return Ok(new { dueToday, offersSent, redeemed, expired });
    }

    // ================================================================
    //  CREATE
    // ================================================================
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] FollowUpRequest req, [FromQuery] int? companyId = null)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (string.IsNullOrWhiteSpace(req.Reason))
            return BadRequest(new { message = "Reason is required." });

        var cid = ResolveCompanyId(companyId);
        await using var tenant = await _tenantFactory.CreateAsync(cid);

        int? creatorId = req.CreatedBy;
        bool isServiceStaff = false;
        if (creatorId.HasValue)
        {
            var creator = await _db.Users.AsNoTracking()
                .FirstOrDefaultAsync(u => u.UserId == creatorId.Value);
            if (creator is not null && creator.RoleId == RoleServiceStaff)
                isServiceStaff = true;
        }

        var customer = await tenant.TenantCustomers
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.TenantCustomerId == req.CustomerId && !c.IsArchived);
        if (customer is null)
            return BadRequest(new { message = $"Customer {req.CustomerId} not found." });

        var scheduled = req.ScheduledDate ?? DateTime.Now;

        var sec = await CRM.api.Services.BranchSecurityHelper.ResolveAndValidateAsync(_db, HttpContext, cid, req.BranchId);
        if (!sec.Allowed) return StatusCode(403, new { message = sec.ErrorMessage });
        int? effectiveBranchId = sec.EffectiveBranchId ?? req.BranchId ?? customer.BranchId;

        var entity = new FollowUp
        {
            CustomerId = req.CustomerId,
            BranchId = effectiveBranchId,
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
                : DeriveStatusOnCreate(scheduled, req.ValidUntil, req.ScheduledNow, req.IsDraft)
        };

        if (!isServiceStaff && !req.IsDraft && req.ScheduledNow)
        {
            if (string.IsNullOrWhiteSpace(customer.EmailAddress))
            {
                entity.Status = "Scheduled";
            }
            else
            {
                var subject = FollowUpEmailTemplate.BuildSubject(BusinessName);
                var html = FollowUpEmailTemplate.BuildHtml(
                    customerName: customer.CustomerName ?? "there",
                    messagePreview: entity.Notes ?? "",
                    discountOffer: entity.DiscountOffer,
                    validUntil: entity.ValidUntil,
                    businessName: BusinessName);

                var sendResult = await _emailSender.SendAsync(
                    toEmail: customer.EmailAddress,
                    toName: customer.CustomerName ?? customer.EmailAddress,
                    subject: subject,
                    htmlBody: html);

                if (sendResult.Success)
                {
                    entity.Status = "Sent";
                    entity.SentAt = DateTime.Now;
                }
                else
                {
                    entity.Status = "Scheduled";
                }
            }
        }
        else if (entity.Status == "Sent")
        {
            entity.SentAt = DateTime.Now;
        }

        try { tenant.FollowUps.Add(entity); await tenant.SaveChangesAsync(); }
        catch (DbUpdateException ex)
        {
            var inner = ex.InnerException?.Message ?? ex.Message;
            return StatusCode(500, new { message = "Create failed.", detail = inner });
        }

        return CreatedAtAction(nameof(GetById), new { id = entity.FollowUpId, companyId = cid }, entity);
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
    public async Task<IActionResult> BulkCreate([FromBody] BulkFollowUpRequest req, [FromQuery] int? companyId = null)
    {
        if (req.CustomerIds == null || req.CustomerIds.Count == 0)
            return BadRequest(new { message = "At least one customer is required." });
        if (string.IsNullOrWhiteSpace(req.Reason))
            return BadRequest(new { message = "Reason is required." });

        var cid = ResolveCompanyId(companyId);
        await using var tenant = await _tenantFactory.CreateAsync(cid);

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

        var existingCustomers = await tenant.TenantCustomers
            .AsNoTracking()
            .Where(c => requestedIds.Contains(c.TenantCustomerId) && !c.IsArchived)
            .ToListAsync();

        var customerMap = existingCustomers.ToDictionary(c => c.TenantCustomerId);
        requestedIds = requestedIds.Where(id => customerMap.ContainsKey(id)).ToList();
        if (requestedIds.Count == 0)
            return BadRequest(new { message = "None of the selected customers exist." });

        // Block only customers who already have an active pending/scheduled follow-up.
        // Completed states (Sent, Contacted, Redeemed, Expired, Draft) do not block future retention follow-ups.
        var blockedIds = await tenant.FollowUps
            .Where(f => requestedIds.Contains(f.CustomerId)
                     && !f.IsArchived
                     && (f.Status == "Scheduled" || f.Status == "Due today" || f.ApprovalStatus == "Pending"))
            .Select(f => f.CustomerId)
            .Distinct()
            .ToListAsync();

        var eligibleIds = requestedIds.Except(blockedIds).ToList();
        if (eligibleIds.Count == 0)
        {
            return Conflict(new
            {
                count = 0,
                sent = 0,
                skipped = blockedIds.Count,
                pendingApproval = 0,
                skippedIds = blockedIds,
                failures = new List<BulkFollowUpFailureDto>(),
                message = "All selected customers already have active follow-ups.",
                blockedCount = blockedIds.Count
            });
        }

        var sec = await CRM.api.Services.BranchSecurityHelper.ResolveAndValidateAsync(_db, HttpContext, cid, req.BranchId);
        if (!sec.Allowed) return StatusCode(403, new { message = sec.ErrorMessage });
        int? effectiveBranchId = sec.EffectiveBranchId ?? req.BranchId;

        var scheduled = req.ScheduledDate ?? DateTime.Now;
        var approvalStatus = isServiceStaff ? "Pending" : "NotRequired";
        var status = isServiceStaff
            ? "Pending"
            : DeriveStatusOnCreate(scheduled, req.ValidUntil, req.ScheduledNow, req.IsDraft);

        bool sendImmediately = !isServiceStaff && !req.IsDraft && req.ScheduledNow;

        var created = new List<FollowUp>(eligibleIds.Count);
        var failures = new List<BulkFollowUpFailureDto>();
        int sentCount = 0;

        foreach (var cId in eligibleIds)
        {
            var cust = customerMap[cId];
            var entity = new FollowUp
            {
                CustomerId = cId,
                BranchId = effectiveBranchId ?? cust.BranchId,
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
                ApprovalStatus = approvalStatus,
                Status = status
            };

            if (sendImmediately)
            {
                if (string.IsNullOrWhiteSpace(cust.EmailAddress))
                {
                    entity.Status = "Scheduled";
                    failures.Add(new BulkFollowUpFailureDto
                    {
                        CustomerId = cId,
                        Error = "Customer has no email address on file."
                    });
                }
                else
                {
                    var subject = FollowUpEmailTemplate.BuildSubject(BusinessName);
                    var html = FollowUpEmailTemplate.BuildHtml(
                        customerName: cust.CustomerName ?? "there",
                        messagePreview: entity.Notes ?? "",
                        discountOffer: entity.DiscountOffer,
                        validUntil: entity.ValidUntil,
                        businessName: BusinessName);

                    var sendResult = await _emailSender.SendAsync(
                        toEmail: cust.EmailAddress,
                        toName: cust.CustomerName ?? cust.EmailAddress,
                        subject: subject,
                        htmlBody: html);

                    if (sendResult.Success)
                    {
                        entity.Status = "Sent";
                        entity.SentAt = DateTime.Now;
                        sentCount++;
                    }
                    else
                    {
                        entity.Status = "Scheduled";
                        failures.Add(new BulkFollowUpFailureDto
                        {
                            CustomerId = cId,
                            Error = sendResult.ErrorMessage ?? "Email send failed."
                        });
                    }
                }
            }
            else if (entity.Status == "Sent")
            {
                entity.SentAt = DateTime.Now;
            }

            tenant.FollowUps.Add(entity);
            created.Add(entity);
        }

        try { await tenant.SaveChangesAsync(); }
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
            pendingApproval = isServiceStaff ? created.Count : 0,
            skippedIds = blockedIds,
            failures = failures,
            createdCount = created.Count,
            blockedCount = blockedIds.Count,
            createdIds = created.Select(f => f.FollowUpId).ToList(),
            message = isServiceStaff
                ? "Submitted for approval."
                : req.IsDraft
                    ? "Saved as draft. No email was sent."
                    : req.ScheduledNow
                        ? $"Follow-ups processed ({sentCount} sent, {failures.Count} failed)."
                        : "Follow-ups scheduled. Email will be sent on the scheduled date."
        });
    }

    // ================================================================
    //  TEST SMTP
    // ================================================================
    [HttpPost("test-smtp")]
    public async Task<IActionResult> TestSmtp([FromQuery] string? to = null)
    {
        var target = string.IsNullOrWhiteSpace(to) ? _smtpOptions.Value.SenderEmail : to;
        var subject = $"AquaShine Car Wash - SMTP Test ({DateTime.Now:yyyy-MM-dd HH:mm:ss})";
        var html = "<div style='font-family:sans-serif;padding:16px;'><h2 style='color:#1E88E5;'>AquaShine CRM SMTP Test</h2><p>This email verifies that your SMTP server and credentials are fully configured and functional.</p></div>";

        var result = await _emailSender.SendAsync(
            toEmail: target,
            toName: "Admin",
            subject: subject,
            htmlBody: html);

        if (result.Success)
        {
            return Ok(new
            {
                success = true,
                message = $"Test email sent successfully to {target}."
            });
        }

        return StatusCode(500, new
        {
            success = false,
            message = result.ErrorMessage
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
    public async Task<IActionResult> Update(int id, [FromBody] FollowUpUpdateRequest req, [FromQuery] int? companyId = null)
    {
        var cid = ResolveCompanyId(companyId);
        await using var tenant = await _tenantFactory.CreateAsync(cid);

        var existing = await tenant.FollowUps.FirstOrDefaultAsync(f => f.FollowUpId == id);
        if (existing is null) return NotFound(new { message = $"FollowUp {id} not found." });

        if (existing.ApprovalStatus == "Pending")
        {
            if (!string.IsNullOrWhiteSpace(req.Type)) existing.Type = Clamp(req.Type, 50, existing.Type);
            if (req.Reason != null) existing.Reason = ClampNullable(req.Reason, 200);
            if (req.DiscountOffer != null) existing.DiscountOffer = ClampNullable(req.DiscountOffer, 200);
            if (req.Notes != null) existing.Notes = ClampNullable(req.Notes, 1000);
            if (req.ScheduledDate.HasValue) existing.ScheduledDate = req.ScheduledDate.Value;
            if (req.ValidUntil.HasValue) existing.ValidUntil = req.ValidUntil.Value;

            await tenant.SaveChangesAsync();
            return Ok(existing);
        }

        if (req.CustomerId.HasValue && req.CustomerId.Value != existing.CustomerId)
        {
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
            await tenant.SaveChangesAsync();
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
                    if (existing.Status != "Sent")
                    {
                        var customer = await tenant.TenantCustomers
                            .AsNoTracking()
                            .FirstOrDefaultAsync(c => c.TenantCustomerId == existing.CustomerId);

                        if (customer != null && !string.IsNullOrWhiteSpace(customer.EmailAddress))
                        {
                            var subject = FollowUpEmailTemplate.BuildSubject(BusinessName);
                            var html = FollowUpEmailTemplate.BuildHtml(
                                customerName: customer.CustomerName ?? "there",
                                messagePreview: existing.Notes ?? "",
                                discountOffer: existing.DiscountOffer,
                                validUntil: existing.ValidUntil,
                                businessName: BusinessName);

                            var sendResult = await _emailSender.SendAsync(
                                toEmail: customer.EmailAddress,
                                toName: customer.CustomerName ?? customer.EmailAddress,
                                subject: subject,
                                htmlBody: html);

                            if (sendResult.Success)
                            {
                                existing.Status = "Sent";
                                existing.SentAt = DateTime.Now;
                            }
                            else
                            {
                                existing.Status = "Scheduled";
                            }
                        }
                        else
                        {
                            existing.Status = "Scheduled";
                        }
                    }
                }

                if (!AllowedStatuses.Contains(existing.Status))
                    existing.Status = "Scheduled";
            }
        }

        try { await tenant.SaveChangesAsync(); }
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
    public async Task<IActionResult> SendEmail(int id, [FromBody] SendFollowUpRequest? req, [FromQuery] int? companyId = null)
    {
        var cid = ResolveCompanyId(req?.CompanyId ?? companyId);
        await using var tenant = await _tenantFactory.CreateAsync(cid);

        var existing = await tenant.FollowUps.FirstOrDefaultAsync(f => f.FollowUpId == id);
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
        await tenant.SaveChangesAsync();

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
    public async Task<IActionResult> SubmitForApproval(int id, [FromBody] SubmitApprovalRequest? req, [FromQuery] int? companyId = null)
    {
        var cid = ResolveCompanyId(companyId);
        await using var tenant = await _tenantFactory.CreateAsync(cid);

        var existing = await tenant.FollowUps.FirstOrDefaultAsync(f => f.FollowUpId == id);
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

        await tenant.SaveChangesAsync();
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
    public async Task<IActionResult> Approve(int id, [FromBody] ApproveRequest req, [FromQuery] int? companyId = null)
    {
        if (req.ApprovedBy <= 0)
            return BadRequest(new { message = "ApprovedBy is required." });

        var cid = ResolveCompanyId(companyId);
        await using var tenant = await _tenantFactory.CreateAsync(cid);

        var approver = await _db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.UserId == req.ApprovedBy);
        if (approver is null) return BadRequest(new { message = "Approver not found." });
        if (approver.RoleId == RoleServiceStaff)
            return Conflict(new { message = "Service Staff cannot approve follow-ups." });

        var existing = await tenant.FollowUps.FirstOrDefaultAsync(f => f.FollowUpId == id);
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
            var customer = await tenant.TenantCustomers
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.TenantCustomerId == existing.CustomerId);

            if (customer is null || string.IsNullOrWhiteSpace(customer.EmailAddress))
            {
                existing.Status = "Scheduled";
                await tenant.SaveChangesAsync();
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
                await tenant.SaveChangesAsync();
                return Ok(new
                {
                    message = "Approved and sent.",
                    followUpId = existing.FollowUpId,
                    status = existing.Status,
                    sentAt = existing.SentAt
                });
            }

            existing.Status = "Scheduled";
            await tenant.SaveChangesAsync();
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
            await tenant.SaveChangesAsync();
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
    public async Task<IActionResult> Reject(int id, [FromBody] RejectRequest req, [FromQuery] int? companyId = null)
    {
        if (req.RejectedBy <= 0)
            return BadRequest(new { message = "RejectedBy is required." });

        var cid = ResolveCompanyId(companyId);
        await using var tenant = await _tenantFactory.CreateAsync(cid);

        var rejector = await _db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.UserId == req.RejectedBy);
        if (rejector is null) return BadRequest(new { message = "Rejector not found." });
        if (rejector.RoleId == RoleServiceStaff)
            return Conflict(new { message = "Service Staff cannot reject follow-ups." });

        var existing = await tenant.FollowUps.FirstOrDefaultAsync(f => f.FollowUpId == id);
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

        await tenant.SaveChangesAsync();
        return Ok(existing);
    }

    // ================================================================
    //  MARK-SENT, REDEEM, ARCHIVE, RESTORE, DELETE
    // ================================================================
    [HttpPost("{id:int}/mark-sent")]
    public async Task<IActionResult> MarkSent(int id, [FromQuery] int? companyId = null)
    {
        var cid = ResolveCompanyId(companyId);
        await using var tenant = await _tenantFactory.CreateAsync(cid);

        var existing = await tenant.FollowUps.FirstOrDefaultAsync(f => f.FollowUpId == id);
        if (existing is null) return NotFound(new { message = $"FollowUp {id} not found." });

        if (existing.ApprovalStatus == "Pending" || existing.ApprovalStatus == "Rejected")
            return Conflict(new { message = "Cannot mark-sent while awaiting approval or after rejection." });
        if (string.Equals(existing.Status, "Redeemed", StringComparison.OrdinalIgnoreCase))
            return Conflict(new { message = "Already redeemed." });

        existing.Status = "Sent";
        existing.SentAt ??= DateTime.Now;

        try { await tenant.SaveChangesAsync(); }
        catch (DbUpdateException ex)
        {
            var inner = ex.InnerException?.Message ?? ex.Message;
            return StatusCode(500, new { message = "Mark-sent failed.", detail = inner });
        }
        return Ok(existing);
    }

    [HttpPost("{id:int}/redeem")]
    public async Task<IActionResult> Redeem(int id, [FromQuery] int? companyId = null)
    {
        var cid = ResolveCompanyId(companyId);
        await using var tenant = await _tenantFactory.CreateAsync(cid);

        var existing = await tenant.FollowUps.FirstOrDefaultAsync(f => f.FollowUpId == id);
        if (existing is null) return NotFound(new { message = $"FollowUp {id} not found." });

        if (existing.ValidUntil.HasValue && existing.ValidUntil.Value.Date < DateTime.Today)
            return Conflict(new { message = "This offer has already expired." });

        existing.Status = "Redeemed";
        try { await tenant.SaveChangesAsync(); }
        catch (DbUpdateException ex)
        {
            var inner = ex.InnerException?.Message ?? ex.Message;
            return StatusCode(500, new { message = "Redeem failed.", detail = inner });
        }
        return Ok(existing);
    }

    public class ArchiveRequest { public string? ArchivedBy { get; set; } }

    [HttpPut("{id:int}/archive")]
    public async Task<IActionResult> Archive(int id, [FromBody] ArchiveRequest? req, [FromQuery] int? companyId = null)
    {
        var cid = ResolveCompanyId(companyId);
        await using var tenant = await _tenantFactory.CreateAsync(cid);

        var existing = await tenant.FollowUps.FirstOrDefaultAsync(f => f.FollowUpId == id);
        if (existing is null) return NotFound(new { message = $"FollowUp {id} not found." });
        if (existing.IsArchived) return Conflict(new { message = "Already archived." });

        existing.IsArchived = true;
        existing.ArchivedAt = DateTime.UtcNow;
        existing.ArchivedBy = string.IsNullOrWhiteSpace(req?.ArchivedBy) ? "Admin" : req!.ArchivedBy;

        try { await tenant.SaveChangesAsync(); }
        catch (DbUpdateException ex)
        {
            var inner = ex.InnerException?.Message ?? ex.Message;
            return StatusCode(500, new { message = "Archive failed.", detail = inner });
        }
        return Ok(existing);
    }

    [HttpPut("{id:int}/restore")]
    public async Task<IActionResult> Restore(int id, [FromQuery] int? companyId = null)
    {
        var cid = ResolveCompanyId(companyId);
        await using var tenant = await _tenantFactory.CreateAsync(cid);

        var existing = await tenant.FollowUps.FirstOrDefaultAsync(f => f.FollowUpId == id);
        if (existing is null) return NotFound(new { message = $"FollowUp {id} not found." });
        if (!existing.IsArchived) return Conflict(new { message = "Not archived." });

        existing.IsArchived = false;
        existing.ArchivedAt = null;
        existing.ArchivedBy = null;

        try { await tenant.SaveChangesAsync(); }
        catch (DbUpdateException ex)
        {
            var inner = ex.InnerException?.Message ?? ex.Message;
            return StatusCode(500, new { message = "Restore failed.", detail = inner });
        }
        return Ok(existing);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, [FromQuery] int? companyId = null)
    {
        var cid = ResolveCompanyId(companyId);
        await using var tenant = await _tenantFactory.CreateAsync(cid);

        var row = await tenant.FollowUps.FirstOrDefaultAsync(f => f.FollowUpId == id);
        if (row is null) return NotFound(new { message = $"FollowUp {id} not found." });

        tenant.FollowUps.Remove(row);
        try { await tenant.SaveChangesAsync(); }
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
    public int? BranchId { get; set; }
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
    public int? BranchId { get; set; }
}

public class BulkFollowUpFailureDto
{
    public int CustomerId { get; set; }
    public string? Error { get; set; }
}