using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CRM.Domain.Entities;
using CRM.domain.Entities;
using CRM.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CRM.api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class BillingController : ControllerBase
    {
        private readonly MasterErpDbContext _db;
        private readonly Microsoft.Extensions.Logging.ILogger<BillingController> _logger;

        public BillingController(MasterErpDbContext db, Microsoft.Extensions.Logging.ILogger<BillingController> logger)
        {
            _db = db;
            _logger = logger;
        }

        // =====================================================================
        // GET /api/billing/summary
        // Returns SaaS subscription metrics for Tenant Businesses
        // =====================================================================
        [HttpGet("summary")]
        public async Task<IActionResult> GetSummary()
        {
            try
            {
                var paidTotal = await _db.TenantBillingTransactions
                    .Where(t => t.PaymentStatus == "Paid" || t.PaymentStatus == "Completed")
                    .SumAsync(t => (decimal?)t.Amount) ?? 0m;

                var outstandingTotal = await _db.TenantBillingTransactions
                    .Where(t => t.PaymentStatus != "Paid" && t.PaymentStatus != "Completed")
                    .SumAsync(t => (decimal?)t.Amount) ?? 0m;

                var activeSubs = await _db.TenantSubscriptions
                    .Where(s => s.Status == "Active")
                    .CountAsync();

                return Ok(new
                {
                    totalPaid = paidTotal,
                    totalPaidFormatted = $"₱{paidTotal:N2}",
                    outstanding = outstandingTotal,
                    outstandingFormatted = $"₱{outstandingTotal:N2}",
                    activeSubscriptions = activeSubs
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to calculate billing summary.");
                return StatusCode(500, new { message = "Failed to calculate billing summary.", error = ex.Message });
            }
        }

        // =====================================================================
        // GET /api/billing/plans
        // =====================================================================
        [HttpGet("plans")]
        public async Task<IActionResult> GetPlans([FromQuery] bool? activeOnly = null, [FromQuery] bool includeArchived = false)
        {
            try
            {
                IQueryable<TenantSubscriptionPlan> query = _db.TenantSubscriptionPlans.AsNoTracking();

                if (!includeArchived)
                {
                    query = query.Where(p => !p.IsArchived);
                }

                if (activeOnly.HasValue && activeOnly.Value)
                {
                    query = query.Where(p => p.IsActive);
                }

                var plans = await query
                    .OrderBy(p => p.PlanId)
                    .Select(p => new
                    {
                        planId = p.PlanId,
                        planName = p.PlanName,
                        price = p.Price,
                        priceFormatted = $"₱{p.Price:N2}",
                        billingCycle = p.BillingCycle,
                        description = p.Description ?? string.Empty,
                        maxUsers = p.MaxUsers,
                        maxCustomers = p.MaxCustomers,
                        multiBranchEnabled = p.MultiBranchEnabled,
                        isActive = p.IsActive,
                        isArchived = p.IsArchived,
                        archivedAt = p.ArchivedAt,
                        activeSubscribers = _db.TenantSubscriptions.Count(s => s.PlanId == p.PlanId && s.Status == "Active")
                    })
                    .ToListAsync();

                return Ok(plans);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load subscription plans.");
                return StatusCode(500, new { message = "Failed to load subscription plans.", error = ex.Message });
            }
        }

        public class SavePlanRequest
        {
            public string PlanName { get; set; } = string.Empty;
            public string? Description { get; set; }
            public decimal Price { get; set; }
            public string BillingCycle { get; set; } = "Monthly";
            public int MaxUsers { get; set; } = 5;
            public int MaxCustomers { get; set; } = 500;
            public bool MultiBranchEnabled { get; set; } = false;
            public bool IsActive { get; set; } = true;
        }

        // =====================================================================
        // POST /api/billing/plans
        // =====================================================================
        [HttpPost("plans")]
        public async Task<IActionResult> CreatePlan([FromBody] SavePlanRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.PlanName))
            {
                return BadRequest("Plan name is required.");
            }

            if (req.Price < 0)
            {
                return BadRequest("Price cannot be negative.");
            }

            var plan = new TenantSubscriptionPlan
            {
                PlanName = req.PlanName.Trim(),
                Description = req.Description?.Trim(),
                Price = req.Price,
                BillingCycle = string.IsNullOrWhiteSpace(req.BillingCycle) ? "Monthly" : req.BillingCycle.Trim(),
                MaxUsers = Math.Max(1, req.MaxUsers),
                MaxCustomers = Math.Max(1, req.MaxCustomers),
                MultiBranchEnabled = req.MultiBranchEnabled,
                IsActive = req.IsActive,
                IsArchived = false,
                CreatedAt = DateTime.UtcNow
            };

            _db.TenantSubscriptionPlans.Add(plan);
            await _db.SaveChangesAsync();

            return Ok(new
            {
                planId = plan.PlanId,
                planName = plan.PlanName,
                price = plan.Price,
                priceFormatted = $"₱{plan.Price:N2}",
                billingCycle = plan.BillingCycle,
                description = plan.Description ?? "",
                maxUsers = plan.MaxUsers,
                maxCustomers = plan.MaxCustomers,
                multiBranchEnabled = plan.MultiBranchEnabled,
                isActive = plan.IsActive,
                isArchived = plan.IsArchived
            });
        }

        // =====================================================================
        // PUT /api/billing/plans/{id}
        // =====================================================================
        [HttpPut("plans/{id:int}")]
        public async Task<IActionResult> UpdatePlan(int id, [FromBody] SavePlanRequest req)
        {
            var plan = await _db.TenantSubscriptionPlans.FirstOrDefaultAsync(p => p.PlanId == id);
            if (plan == null)
            {
                return NotFound($"Plan {id} not found.");
            }

            if (string.IsNullOrWhiteSpace(req.PlanName))
            {
                return BadRequest("Plan name is required.");
            }

            if (req.Price < 0)
            {
                return BadRequest("Price cannot be negative.");
            }

            plan.PlanName = req.PlanName.Trim();
            plan.Description = req.Description?.Trim();
            plan.Price = req.Price;
            plan.BillingCycle = string.IsNullOrWhiteSpace(req.BillingCycle) ? "Monthly" : req.BillingCycle.Trim();
            plan.MaxUsers = Math.Max(1, req.MaxUsers);
            plan.MaxCustomers = Math.Max(1, req.MaxCustomers);
            plan.MultiBranchEnabled = req.MultiBranchEnabled;
            plan.IsActive = req.IsActive;

            await _db.SaveChangesAsync();

            return Ok(new
            {
                planId = plan.PlanId,
                planName = plan.PlanName,
                price = plan.Price,
                priceFormatted = $"₱{plan.Price:N2}",
                billingCycle = plan.BillingCycle,
                description = plan.Description ?? "",
                maxUsers = plan.MaxUsers,
                maxCustomers = plan.MaxCustomers,
                multiBranchEnabled = plan.MultiBranchEnabled,
                isActive = plan.IsActive,
                isArchived = plan.IsArchived
            });
        }

        // =====================================================================
        // PUT /api/billing/plans/{id}/status
        // =====================================================================
        public class PlanStatusRequest
        {
            public bool? IsActive { get; set; }
        }

        [HttpPut("plans/{id:int}/status")]
        public async Task<IActionResult> TogglePlanStatus(int id, [FromBody] PlanStatusRequest? req)
        {
            var plan = await _db.TenantSubscriptionPlans.FirstOrDefaultAsync(p => p.PlanId == id);
            if (plan == null)
            {
                return NotFound($"Plan {id} not found.");
            }

            plan.IsActive = req?.IsActive ?? !plan.IsActive;
            await _db.SaveChangesAsync();

            return Ok(new
            {
                planId = plan.PlanId,
                isActive = plan.IsActive,
                message = plan.IsActive ? "Plan activated." : "Plan deactivated."
            });
        }

        // =====================================================================
        // PUT /api/billing/plans/{id}/archive
        // =====================================================================
        [HttpPut("plans/{id:int}/archive")]
        public async Task<IActionResult> ToggleArchivePlan(int id)
        {
            var plan = await _db.TenantSubscriptionPlans.FirstOrDefaultAsync(p => p.PlanId == id);
            if (plan == null)
            {
                return NotFound($"Plan {id} not found.");
            }

            plan.IsArchived = !plan.IsArchived;
            plan.ArchivedAt = plan.IsArchived ? DateTime.UtcNow : null;
            if (plan.IsArchived)
            {
                plan.IsActive = false;
            }

            await _db.SaveChangesAsync();

            return Ok(new
            {
                planId = plan.PlanId,
                isArchived = plan.IsArchived,
                isActive = plan.IsActive,
                message = plan.IsArchived ? "Plan archived successfully." : "Plan unarchived successfully."
            });
        }

        // =====================================================================
        // DELETE /api/billing/plans/{id}
        // =====================================================================
        [HttpDelete("plans/{id:int}")]
        public async Task<IActionResult> DeletePlan(int id)
        {
            var plan = await _db.TenantSubscriptionPlans.FirstOrDefaultAsync(p => p.PlanId == id);
            if (plan == null)
            {
                return NotFound($"Plan {id} not found.");
            }

            var hasSubs = await _db.TenantSubscriptions.AnyAsync(s => s.PlanId == id);
            if (hasSubs)
            {
                // Soft archive
                plan.IsArchived = true;
                plan.IsActive = false;
                plan.ArchivedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync();
                return Ok(new { message = "Plan has active subscribers so it was archived instead of deleted." });
            }

            _db.TenantSubscriptionPlans.Remove(plan);
            await _db.SaveChangesAsync();
            return NoContent();
        }

        // =====================================================================
        // GET /api/billing/customer-subscriptions
        // Returns Tenant Company subscriptions managed by Admin users
        // =====================================================================
        [HttpGet("customer-subscriptions")]
        public async Task<IActionResult> GetCustomerSubscriptions()
        {
            try
            {
                var subs = await _db.TenantSubscriptions
                    .AsNoTracking()
                    .Include(s => s.Company)
                    .Include(s => s.Plan)
                    .OrderByDescending(s => s.TenantSubscriptionId)
                    .ToListAsync();

                var companyIds = subs.Select(s => s.CompanyId).Distinct().ToList();
                var admins = await _db.Users
                    .AsNoTracking()
                    .Where(u => u.CompanyId != null && companyIds.Contains(u.CompanyId.Value) && (u.RoleId == 1 || u.RoleId == 2))
                    .OrderBy(u => u.RoleId)
                    .ToListAsync();

                var result = subs.Select(s =>
                {
                    var comp = s.Company;
                    var admin = admins.FirstOrDefault(a => a.CompanyId == s.CompanyId);

                    return new
                    {
                        subscriptionId = s.TenantSubscriptionId,
                        companyId = s.CompanyId,
                        companyCode = comp?.CompanyCode ?? $"COMP{s.CompanyId:D3}",
                        tenantCompany = comp?.CompanyName ?? $"Company #{s.CompanyId}",
                        adminUser = admin?.FullName ?? "Unassigned",
                        adminEmail = admin?.Email ?? "",
                        planId = s.PlanId ?? 0,
                        planName = s.Plan != null ? s.Plan.PlanName : "No Plan Assigned",
                        billingCycle = s.Plan != null ? s.Plan.BillingCycle : "Monthly",
                        price = s.Plan != null ? s.Plan.Price : 0m,
                        priceFormatted = $"₱{(s.Plan != null ? s.Plan.Price : 0m):N2}",
                        managedBy = 1,
                        managedByName = "Super Admin",
                        startDate = s.StartDate ?? s.CreatedAt,
                        endDate = s.RenewalDate,
                        status = s.Status
                    };
                }).ToList();

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load tenant subscriptions in Super Admin.");
                return StatusCode(500, new { message = "Failed to load customer subscriptions.", error = ex.Message });
            }
        }

        public class AssignPlanRequest
        {
            public int CompanyId { get; set; }
            public int PlanId { get; set; }
            public string Status { get; set; } = "Active";
            public DateTime? StartDate { get; set; }
            public DateTime? RenewalDate { get; set; }
            public bool AutoRenew { get; set; } = true;
        }

        // =====================================================================
        // POST /api/billing/assign-plan
        // =====================================================================
        [HttpPost("assign-plan")]
        public async Task<IActionResult> AssignPlan([FromBody] AssignPlanRequest req)
        {
            try
            {
                var comp = await _db.Companies.FirstOrDefaultAsync(c => c.CompanyId == req.CompanyId);
                if (comp == null)
                {
                    return NotFound($"Company {req.CompanyId} not found.");
                }

                var plan = await _db.TenantSubscriptionPlans.FirstOrDefaultAsync(p => p.PlanId == req.PlanId);
                if (plan == null)
                {
                    return NotFound($"Subscription plan {req.PlanId} not found.");
                }

                var sub = await _db.TenantSubscriptions
                    .FirstOrDefaultAsync(s => s.CompanyId == req.CompanyId);

                DateTime start = req.StartDate ?? DateTime.UtcNow;
                DateTime renewal = req.RenewalDate ?? start.AddMonths(1);

                if (sub == null)
                {
                    sub = new TenantSubscription
                    {
                        CompanyId = req.CompanyId,
                        PlanId = req.PlanId,
                        Status = req.Status,
                        StartDate = start,
                        RenewalDate = renewal,
                        AutoRenew = req.AutoRenew,
                        CreatedAt = DateTime.UtcNow
                    };
                    _db.TenantSubscriptions.Add(sub);
                }
                else
                {
                    sub.PlanId = req.PlanId;
                    sub.Status = req.Status;
                    sub.StartDate = start;
                    sub.RenewalDate = renewal;
                    sub.AutoRenew = req.AutoRenew;
                }

                await _db.SaveChangesAsync();

                // Create pending or completed transaction entry for billing record
                var txn = new TenantBillingTransaction
                {
                    TenantSubscriptionId = sub.TenantSubscriptionId,
                    CompanyId = req.CompanyId,
                    Amount = plan.Price,
                    PaymentStatus = "Pending",
                    TransactionDate = DateTime.UtcNow,
                    ReferenceNumber = $"INV-{DateTime.UtcNow:yyyyMMdd}-{sub.TenantSubscriptionId:D4}"
                };
                _db.TenantBillingTransactions.Add(txn);
                await _db.SaveChangesAsync();

                return Ok(new
                {
                    subscriptionId = sub.TenantSubscriptionId,
                    companyId = sub.CompanyId,
                    planId = sub.PlanId,
                    status = sub.Status,
                    startDate = sub.StartDate,
                    renewalDate = sub.RenewalDate
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to assign plan to company {CompanyId}.", req.CompanyId);
                return StatusCode(500, new { message = "Failed to assign subscription plan.", error = ex.Message });
            }
        }

        // =====================================================================
        // GET /api/billing/transactions
        // =====================================================================
        [HttpGet("transactions")]
        public async Task<IActionResult> GetTransactions()
        {
            try
            {
                var rawTxs = await _db.TenantBillingTransactions
                    .AsNoTracking()
                    .Include(t => t.Company)
                    .Include(t => t.TenantSubscription)
                        .ThenInclude(ts => ts!.Plan)
                    .OrderByDescending(t => t.TransactionDate)
                    .ThenByDescending(t => t.TransactionId)
                    .ToListAsync();

                var result = rawTxs.Select(t =>
                {
                    var comp = t.Company;
                    var plan = t.TenantSubscription?.Plan;
                    var planName = plan?.PlanName ?? "Subscription Plan";
                    var refCode = !string.IsNullOrWhiteSpace(t.ReferenceNumber)
                        ? t.ReferenceNumber
                        : $"SUB-INV-{t.TransactionDate:yyyyMM}-{t.TransactionId:D4}";

                    return new
                    {
                        transactionId = t.TransactionId,
                        subscriptionId = t.TenantSubscriptionId,
                        tenantCompany = comp?.CompanyName ?? $"Company #{t.CompanyId}",
                        tenantCompanyCode = comp?.CompanyCode ?? $"COMP{t.CompanyId:D3}",
                        planName = planName,
                        referenceNumber = refCode,
                        amount = t.Amount,
                        amountFormatted = $"₱{t.Amount:N2}",
                        paymentStatus = t.PaymentStatus,
                        paymentMethod = t.PaymentMethod ?? "—",
                        transactionDate = t.TransactionDate
                    };
                }).ToList();

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load billing transactions.");
                return StatusCode(500, new { message = "Failed to load billing transactions.", error = ex.Message });
            }
        }

        public class MarkPaidRequest
        {
            public int TransactionId { get; set; }
            public string? PaymentMethod { get; set; }
            public string? ReferenceNumber { get; set; }
        }

        // =====================================================================
        // POST /api/billing/mark-paid
        // =====================================================================
        [HttpPost("mark-paid")]
        public async Task<IActionResult> MarkPaid([FromBody] MarkPaidRequest req)
        {
            try
            {
                var txn = await _db.TenantBillingTransactions.FirstOrDefaultAsync(t => t.TransactionId == req.TransactionId);
                if (txn == null)
                {
                    return NotFound($"Transaction {req.TransactionId} not found.");
                }

                txn.PaymentStatus = "Paid";
                txn.PaymentMethod = string.IsNullOrWhiteSpace(req.PaymentMethod) ? "Cash / Manual" : req.PaymentMethod.Trim();
                if (!string.IsNullOrWhiteSpace(req.ReferenceNumber))
                {
                    txn.ReferenceNumber = req.ReferenceNumber.Trim();
                }
                txn.TransactionDate = DateTime.UtcNow;

                await _db.SaveChangesAsync();

                return Ok(new
                {
                    transactionId = txn.TransactionId,
                    paymentStatus = txn.PaymentStatus,
                    paymentMethod = txn.PaymentMethod,
                    referenceNumber = txn.ReferenceNumber
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to mark transaction {TransactionId} as paid.", req.TransactionId);
                return StatusCode(500, new { message = "Failed to record payment.", error = ex.Message });
            }
        }
    }
}
