using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CRM.Domain.Entities;
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

        public BillingController(MasterErpDbContext db)
        {
            _db = db;
        }

        // =====================================================================
        // GET /api/billing/summary
        // Returns ONLY tenant CRM subscription metrics (excludes retail carwash jobs)
        // =====================================================================
        [HttpGet("summary")]
        public async Task<IActionResult> GetSummary()
        {
            var paidTotal = await _db.BillingTransactions
                .Where(t => t.CustomerSubscriptionId != null && t.PaymentStatus == "Paid")
                .SumAsync(t => (decimal?)t.Amount) ?? 0m;

            var outstandingTotal = await _db.BillingTransactions
                .Where(t => t.CustomerSubscriptionId != null && t.PaymentStatus != "Paid")
                .SumAsync(t => (decimal?)t.Amount) ?? 0m;

            var activeSubs = await _db.CustomerSubscriptions
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

        // =====================================================================
        // GET /api/billing/plans
        // =====================================================================
        [HttpGet("plans")]
        public async Task<IActionResult> GetPlans()
        {
            var plans = await _db.SubscriptionPlans
                .AsNoTracking()
                .OrderBy(p => p.PlanId)
                .Select(p => new
                {
                    planId = p.PlanId,
                    planName = p.PlanName,
                    price = p.Price,
                    priceFormatted = $"₱{p.Price:N2}",
                    billingCycle = p.BillingCycle,
                    description = p.Description,
                    activeSubscribers = _db.CustomerSubscriptions.Count(s => s.PlanId == p.PlanId && s.Status == "Active")
                })
                .ToListAsync();

            return Ok(plans);
        }

        // =====================================================================
        // GET /api/billing/customer-subscriptions
        // Returns Tenant Company subscriptions managed by Admin users
        // =====================================================================
        [HttpGet("customer-subscriptions")]
        public async Task<IActionResult> GetCustomerSubscriptions()
        {
            var companies = await _db.Companies
                .AsNoTracking()
                .OrderBy(c => c.CompanyId)
                .ToListAsync();

            var adminUsers = await _db.Users
                .AsNoTracking()
                .Where(u => u.RoleId == 1)
                .ToListAsync();

            var defaultAdmin = adminUsers.FirstOrDefault() ??
                await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.RoleId == 4) ??
                new CRM.domain.Entities.User { FullName = "System Administrator", Email = "admin@aquashine.com" };

            var subs = await _db.CustomerSubscriptions
                .AsNoTracking()
                .Include(s => s.Plan)
                .Include(s => s.ManagedByUser)
                .OrderByDescending(s => s.SubscriptionId)
                .ToListAsync();

            var result = subs.Select(s =>
            {
                var comp = ResolveCompany(companies, s.CustomerId, s.SubscriptionId);
                var compName = ResolveCompanyName(comp);
                var admin = ResolveAdmin(adminUsers, comp.CompanyId, defaultAdmin);

                return new
                {
                    subscriptionId = s.SubscriptionId,
                    companyId = comp.CompanyId,
                    companyCode = comp.CompanyCode,
                    tenantCompany = compName,
                    adminUser = admin.FullName,
                    adminEmail = admin.Email,
                    planId = s.PlanId,
                    planName = s.Plan != null ? s.Plan.PlanName : "Plan #" + s.PlanId,
                    billingCycle = s.Plan != null ? s.Plan.BillingCycle : "Monthly",
                    price = s.Plan != null ? s.Plan.Price : 0m,
                    priceFormatted = $"₱{(s.Plan != null ? s.Plan.Price : 0m):N2}",
                    managedBy = s.ManagedBy,
                    managedByName = s.ManagedByUser != null ? s.ManagedByUser.FullName : admin.FullName,
                    startDate = s.StartDate,
                    endDate = s.EndDate,
                    status = s.Status
                };
            }).ToList();

            return Ok(result);
        }

        // =====================================================================
        // GET /api/billing/transactions
        // Returns ONLY SaaS subscription billing transactions (excludes carwash service jobs)
        // =====================================================================
        [HttpGet("transactions")]
        public async Task<IActionResult> GetTransactions()
        {
            var companies = await _db.Companies
                .AsNoTracking()
                .OrderBy(c => c.CompanyId)
                .ToListAsync();

            var rawTxs = await _db.BillingTransactions
                .AsNoTracking()
                .Include(t => t.CustomerSubscription)
                    .ThenInclude(cs => cs.Plan)
                .Where(t => t.CustomerSubscriptionId != null)
                .OrderByDescending(t => t.TransactionDate)
                .ThenByDescending(t => t.TransactionId)
                .ToListAsync();

            var result = rawTxs.Select(t =>
            {
                var sub = t.CustomerSubscription;
                var comp = ResolveCompany(companies, sub?.CustomerId ?? 1, sub?.SubscriptionId ?? 1);
                var compName = ResolveCompanyName(comp);
                var planName = sub?.Plan?.PlanName ?? "Subscription Plan";
                var refCode = $"SUB-INV-{t.TransactionDate:yyyyMM}-{t.TransactionId:D4}";

                return new
                {
                    transactionId = t.TransactionId,
                    subscriptionId = t.CustomerSubscriptionId,
                    tenantCompany = compName,
                    tenantCompanyCode = comp.CompanyCode,
                    planName = planName,
                    referenceNumber = refCode,
                    amount = t.Amount,
                    amountFormatted = $"₱{t.Amount:N2}",
                    paymentStatus = t.PaymentStatus,
                    transactionDate = t.TransactionDate
                };
            }).ToList();

            return Ok(result);
        }

        // =====================================================================
        // Helpers for multi-tenant resolution
        // =====================================================================
        private static Company ResolveCompany(List<Company> companies, int customerId, int subscriptionId)
        {
            if (companies == null || companies.Count == 0)
            {
                return new Company { CompanyId = 1, CompanyCode = "COMP001", CompanyName = "AquaShine Car Wash" };
            }

            var match = companies.FirstOrDefault(c => c.CompanyId == customerId);
            if (match != null) return match;

            int index = Math.Abs((subscriptionId - 1) % companies.Count);
            return companies[index];
        }

        private static string ResolveCompanyName(Company comp)
        {
            if (comp.CompanyCode == "COMP001" || comp.CompanyName.Equals("My First Company", StringComparison.OrdinalIgnoreCase))
            {
                return "AquaShine Car Wash";
            }
            return string.IsNullOrWhiteSpace(comp.CompanyName) ? $"Company {comp.CompanyCode}" : comp.CompanyName;
        }

        private static CRM.domain.Entities.User ResolveAdmin(List<CRM.domain.Entities.User> adminUsers, int companyId, CRM.domain.Entities.User defaultAdmin)
        {
            if (adminUsers == null || adminUsers.Count == 0) return defaultAdmin;
            int index = Math.Abs((companyId - 1) % adminUsers.Count);
            return adminUsers[index];
        }
    }
}
