using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CRM.Domain.Entities;
using CRM.Infrastructure.Data;
using CRM.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CRM.api.Controllers;

[ApiController]
[Route("api/analytics")]
public class AnalyticsController : ControllerBase
{
    private readonly MasterErpDbContext _db;
    private readonly ITenantDbContextFactory _tenantFactory;

    public AnalyticsController(MasterErpDbContext db, ITenantDbContextFactory tenantFactory)
    {
        _db = db;
        _tenantFactory = tenantFactory;
    }

    // GET: api/analytics/summary?companyId=1
    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary(
        [FromQuery] int companyId = 1,
        [FromQuery] int? branchId = null)
    {
        var sec = await CRM.api.Services.BranchSecurityHelper.ResolveAndValidateAsync(_db, HttpContext, companyId, branchId);
        if (!sec.Allowed)
        {
            return StatusCode(403, new { message = sec.ErrorMessage });
        }
        branchId = sec.EffectiveBranchId;

        var today = DateTime.Today;
        var monthStart = new DateTime(today.Year, today.Month, 1);

        var tenant = await _tenantFactory.CreateAsync(companyId);

        // Only active customers count toward the metrics.
        var customersQuery = tenant.TenantCustomers
            .AsNoTracking()
            .Where(c => !c.IsArchived);

        if (branchId.HasValue && branchId.Value > 0)
        {
            customersQuery = customersQuery.Where(c => c.BranchId == branchId.Value);
        }

        var customers = await customersQuery.ToListAsync();

        var totalCustomers = customers.Count;

        // Only active service requests count.
        var requestsQuery = tenant.ServiceRequests
            .AsNoTracking()
            .Where(r => !r.IsArchived);

        if (branchId.HasValue && branchId.Value > 0)
        {
            requestsQuery = requestsQuery.Where(r => r.BranchId == branchId.Value);
        }

        var requests = await requestsQuery.ToListAsync();

        var byCustomer = requests
            .Where(r => r.CustomerId > 0)
            .GroupBy(r => r.CustomerId)
            .Select(g => new { Id = g.Key, Count = g.Count() })
            .ToList();

        var returning = byCustomer.Count(x => x.Count > 1);

        var carsWashedMonth = requests.Count(r =>
            r.Status == "Completed"
            && r.CompletedDate.HasValue
            && r.CompletedDate.Value >= monthStart);

        // Only active services are used for pricing.
        var tenantProducts = await tenant.Products
            .AsNoTracking()
            .Where(p => !p.IsArchived)
            .ToListAsync();

        var priceLookup = tenantProducts
            .GroupBy(p => p.ProductId)
            .ToDictionary(g => g.Key, g => g.First().UnitPrice);

        decimal revenueMonth = 0m;
        foreach (var r in requests.Where(r => r.Status == "Completed" && r.CompletedDate.HasValue
                                           && r.CompletedDate.Value >= monthStart))
        {
            if (priceLookup.TryGetValue(r.ServiceId, out var price))
                revenueMonth += price;
        }

        decimal totalRevenue = 0m;
        foreach (var r in requests.Where(r => r.Status == "Completed"))
        {
            if (priceLookup.TryGetValue(r.ServiceId, out var price))
                totalRevenue += price;
        }

        decimal avgSpend = totalCustomers == 0 ? 0m : Math.Round(totalRevenue / totalCustomers, 2);

        var segments = SegmentCustomers(customers, requests);
        var lost = segments.Count(s => s.Segment == "Lost");
        double churnRate = totalCustomers == 0 ? 0 : Math.Round(lost * 100.0 / totalCustomers, 1);

        return Ok(new
        {
            totalCustomers,
            returningCustomers = returning,
            churnRate,
            avgSpendPerCustomer = avgSpend,
            carsWashedThisMonth = carsWashedMonth,
            revenueThisMonth = revenueMonth
        });
    }

    // GET: api/analytics/retention?companyId=1
    [HttpGet("retention")]
    public async Task<IActionResult> GetRetention([FromQuery] int companyId = 1)
    {
        var today = DateTime.Today;

        var tenant = await _tenantFactory.CreateAsync(companyId);

        var customers = await tenant.TenantCustomers
            .AsNoTracking()
            .Where(c => !c.IsArchived)
            .ToListAsync();

        var requests = await tenant.ServiceRequests
            .AsNoTracking()
            .Where(r => !r.IsArchived)
            .ToListAsync();

        var months = new List<object>();
        for (int i = 8; i >= 0; i--)
        {
            var m = new DateTime(today.Year, today.Month, 1).AddMonths(-i);
            var monthEnd = m.AddMonths(1).AddDays(-1);

            var active = 0;
            foreach (var c in customers)
            {
                var visits = requests
                    .Where(r => r.CustomerId == c.TenantCustomerId)
                    .OrderBy(r => r.RequestedDate)
                    .ToList();

                if (visits.Count < 2) continue;

                var inMonth = visits.Where(v => v.RequestedDate >= m && v.RequestedDate <= monthEnd).ToList();
                if (inMonth.Count == 0) continue;

                var lastInMonth = inMonth.Max(v => v.RequestedDate);
                var nextVisit = visits.FirstOrDefault(v => v.RequestedDate > lastInMonth
                                                       && v.RequestedDate <= lastInMonth.AddDays(90));
                if (nextVisit != null) active++;
            }

            months.Add(new { month = m.ToString("MMM"), value = active });
        }

        return Ok(months);
    }

    // GET: api/analytics/segments?companyId=1
    [HttpGet("segments")]
    public async Task<IActionResult> GetSegments([FromQuery] int companyId = 1)
    {
        var tenant = await _tenantFactory.CreateAsync(companyId);

        var customers = await tenant.TenantCustomers
            .AsNoTracking()
            .Where(c => !c.IsArchived)
            .ToListAsync();

        var requests = await tenant.ServiceRequests
            .AsNoTracking()
            .Where(r => !r.IsArchived)
            .ToListAsync();

        var segments = SegmentCustomers(customers, requests);

        var total = segments.Count;
        var active = segments.Count(s => s.Segment == "Active");
        var atRisk = segments.Count(s => s.Segment == "AtRisk");
        var lost = segments.Count(s => s.Segment == "Lost");

        double activePct = total == 0 ? 0 : Math.Round(active * 100.0 / total, 1);
        double riskPct = total == 0 ? 0 : Math.Round(atRisk * 100.0 / total, 1);
        double lostPct = total == 0 ? 0 : Math.Round(lost * 100.0 / total, 1);

        return Ok(new
        {
            total,
            active,
            activePct,
            atRisk,
            atRiskPct = riskPct,
            lost,
            lostPct
        });
    }

    // GET: api/analytics/segment-customers?companyId=1&segment=AtRisk&branchId=2
    [HttpGet("segment-customers")]
    public async Task<IActionResult> GetSegmentCustomers(
        [FromQuery] int companyId = 1,
        [FromQuery] string segment = "AtRisk",
        [FromQuery] int? branchId = null)
    {
        var sec = await CRM.api.Services.BranchSecurityHelper.ResolveAndValidateAsync(_db, HttpContext, companyId, branchId);
        if (!sec.Allowed) return StatusCode(403, new { message = sec.ErrorMessage });
        branchId = sec.EffectiveBranchId;

        var tenant = await _tenantFactory.CreateAsync(companyId);

        var custQuery = tenant.TenantCustomers
            .AsNoTracking()
            .Where(c => !c.IsArchived);

        if (branchId.HasValue && branchId.Value > 0)
        {
            custQuery = custQuery.Where(c => c.BranchId == branchId.Value);
        }

        var customers = await custQuery.ToListAsync();

        var reqQuery = tenant.ServiceRequests
            .AsNoTracking()
            .Where(r => !r.IsArchived);

        if (branchId.HasValue && branchId.Value > 0)
        {
            reqQuery = reqQuery.Where(r => r.BranchId == branchId.Value);
        }

        var requests = await reqQuery.ToListAsync();

        var allRows = SegmentCustomers(customers, requests);
        var rows = (string.IsNullOrWhiteSpace(segment) || string.Equals(segment, "All", StringComparison.OrdinalIgnoreCase))
            ? allRows
            : allRows.Where(s => string.Equals(s.Segment, segment, StringComparison.OrdinalIgnoreCase)).ToList();

        if (rows.Count > 0)
        {
            var ids = rows.Select(r => r.CustomerId).ToHashSet();

            var latestFollowUps = await tenant.FollowUps
                .AsNoTracking()
                .Where(f => ids.Contains(f.CustomerId) && !f.IsArchived)
                .GroupBy(f => f.CustomerId)
                .Select(g => g.OrderByDescending(f => f.FollowUpId).First())
                .ToListAsync();

            var byCustomer = latestFollowUps.ToDictionary(f => f.CustomerId);

            foreach (var r in rows)
            {
                if (!byCustomer.TryGetValue(r.CustomerId, out var f)) continue;

                r.LastFollowUpId = f.FollowUpId;
                r.LastFollowUpStatus = f.Status;
                r.LastFollowUpType = f.Type;
                r.LastFollowUpDate = f.ScheduledDate;

                bool isBlockingStatus =
                    !string.Equals(f.Status, "Expired", StringComparison.OrdinalIgnoreCase);

                r.HasOpenFollowUp = isBlockingStatus;
            }
        }

        return Ok(rows);
    }

    // GET: api/analytics/revenue?companyId=1
    [HttpGet("revenue")]
    public async Task<IActionResult> GetRevenue([FromQuery] int companyId = 1)
    {
        var today = DateTime.Today;
        var tenant = await _tenantFactory.CreateAsync(companyId);

        var products = await tenant.Products
            .AsNoTracking()
            .Where(p => !p.IsArchived)
            .ToListAsync();

        var archivedServiceIds = await tenant.Products
            .AsNoTracking()
            .Where(p => p.IsArchived)
            .Select(p => p.ProductId)
            .ToHashSetAsync();

        var priceLookup = products
            .GroupBy(p => p.ProductId)
            .ToDictionary(g => g.Key, g => g.First().UnitPrice);

        // Only non-archived completed requests whose service is still active.
        var requests = await tenant.ServiceRequests
            .AsNoTracking()
            .Where(r => !r.IsArchived
                     && r.Status == "Completed"
                     && r.CompletedDate != null
                     && !archivedServiceIds.Contains(r.ServiceId))
            .ToListAsync();

        var rows = new List<object>();
        decimal ytd = 0m;
        string bestMonth = "";
        decimal bestVal = 0m;

        for (int month = 1; month <= today.Month; month++)
        {
            var m = new DateTime(today.Year, month, 1);
            var monthEnd = m.AddMonths(1).AddDays(-1);

            decimal total = 0m;
            foreach (var r in requests.Where(r => r.CompletedDate.HasValue
                                              && r.CompletedDate.Value >= m
                                              && r.CompletedDate.Value <= monthEnd))
            {
                if (priceLookup.TryGetValue(r.ServiceId, out var price))
                    total += price;
            }

            ytd += total;
            if (total > bestVal) { bestVal = total; bestMonth = m.ToString("MMM ''yy"); }

            rows.Add(new
            {
                label = m.ToString("MMM ''yy"),
                value = total
            });
        }

        return Ok(new
        {
            months = rows,
            ytd,
            bestMonth,
            bestValue = bestVal
        });
    }

    // GET: api/analytics/recent
    [HttpGet("recent")]
    public async Task<IActionResult> GetRecent([FromQuery] int companyId = 1)
    {
        var tenant = await _tenantFactory.CreateAsync(companyId);
        var list = await tenant.ServiceRequests
            .AsNoTracking()
            .Where(r => !r.IsArchived)
            .OrderByDescending(r => r.RequestId)
            .Take(8)
            .ToListAsync();

        return Ok(list);
    }

    // ================================================================
    //  WASH FREQUENCY BY CUSTOMER SEGMENT
    //  ----------------------------------------------------------------
    //  Returns the average completed washes per month for each loyalty
    //  tier. Only completed, non-archived service requests inside the
    //  requested range are counted — the same definition of "completed
    //  transaction" used by the Reports module.
    //
    //  Tiers (based on completed visits inside the period):
    //      New         1 visit
    //      Occasional  2 – 4
    //      Regular     5 – 9
    //      Loyal       10+
    // ================================================================
    [HttpGet("wash-frequency")]
    public async Task<IActionResult> GetWashFrequency(
        [FromQuery] int companyId = 1,
        [FromQuery] string range = "ThisYear")
    {
        var today = DateTime.Today;
        var (from, to) = ResolveAnalyticsRange(range, today);

        var tenant = await _tenantFactory.CreateAsync(companyId);

        var customers = await tenant.TenantCustomers
            .AsNoTracking()
            .Where(c => !c.IsArchived)
            .ToListAsync();

        // Only completed, non-archived service requests inside the period.
        var completed = await tenant.ServiceRequests
            .AsNoTracking()
            .Where(r => !r.IsArchived
                     && r.Status == "Completed"
                     && r.CompletedDate != null
                     && r.CompletedDate >= from
                     && r.CompletedDate <= to)
            .ToListAsync();

        // Group completed visits by customer for the period.
        var visitsByCustomer = completed
            .Where(r => r.CustomerId > 0)
            .GroupBy(r => r.CustomerId)
            .ToDictionary(g => g.Key, g => g.Count());

        // Months in the period, inclusive. Minimum 1 to avoid divide-by-zero.
        int months = Math.Max(1,
            ((to.Year - from.Year) * 12) + to.Month - from.Month + 1);

        // Bucket customers by their total visit count for the period.
        var tiers = new Dictionary<string, (int Customers, int Visits)>
        {
            ["New"] = (0, 0),
            ["Occasional"] = (0, 0),
            ["Regular"] = (0, 0),
            ["Loyal"] = (0, 0)
        };

        foreach (var c in customers)
        {
            visitsByCustomer.TryGetValue(c.TenantCustomerId, out var visits);

            // Customers with 0 visits in the period don't appear on the chart —
            // they'd just skew every segment down to zero.
            if (visits == 0) continue;

            string tier = ClassifyTier(visits);
            var cur = tiers[tier];
            tiers[tier] = (cur.Customers + 1, cur.Visits + visits);
        }

        var order = new[] { "New", "Occasional", "Regular", "Loyal" };
        var result = new List<object>(order.Length);

        foreach (var label in order)
        {
            var t = tiers[label];
            double avg = t.Customers == 0
                ? 0.0
                : Math.Round((double)t.Visits / t.Customers / months, 1);

            result.Add(new
            {
                label,
                value = avg,
                customerCount = t.Customers,
                segmentKey = label
            });
        }

        return Ok(result);
    }

    // ================================================================
    //  HELPERS
    // ================================================================
    private class SegmentRow
    {
        public int CustomerId { get; set; }
        public string Name { get; set; } = "";
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? Vehicle { get; set; }
        public DateTime LastVisit { get; set; }
        public int DaysSince { get; set; }
        public string Segment { get; set; } = "Active";
        public int DaysLeft { get; set; }

        public bool HasOpenFollowUp { get; set; }
        public string? LastFollowUpStatus { get; set; }
        public string? LastFollowUpType { get; set; }
        public DateTime? LastFollowUpDate { get; set; }
        public int? LastFollowUpId { get; set; }
    }

    private static List<SegmentRow> SegmentCustomers(
        List<CRM.Domain.Entities.TenantCustomer> customers,
        List<CRM.domain.Entities.ServiceRequest> requests)
    {
        var today = DateTime.Today;
        var rows = new List<SegmentRow>();

        foreach (var c in customers)
        {
            var visits = requests
                .Where(r => r.CustomerId == c.TenantCustomerId)
                .Select(r => r.RequestedDate)
                .OrderByDescending(d => d)
                .ToList();

            var last = visits.FirstOrDefault();
            int days = last == default ? 9999 : (today - last.Date).Days;

            string seg = days <= 60 ? "Active" : days <= 120 ? "AtRisk" : "Lost";

            int daysLeft = seg == "Lost" ? -1 : (seg == "AtRisk" ? 120 - days : 60 - days);

            rows.Add(new SegmentRow
            {
                CustomerId = c.TenantCustomerId,
                Name = c.CustomerName ?? $"id:{c.TenantCustomerId}",
                Phone = c.ContactNumber,
                Email = c.EmailAddress,
                Vehicle = string.IsNullOrWhiteSpace(c.VehicleType) ? null : c.VehicleType,
                LastVisit = last,
                DaysSince = days,
                Segment = seg,
                DaysLeft = daysLeft
            });
        }

        return rows;
    }

    // Tier thresholds — tune here if the business rules change.
    // Visits counted are only the completed ones inside the selected period.
    private static string ClassifyTier(int visits)
    {
        if (visits <= 1) return "New";
        if (visits <= 4) return "Occasional";
        if (visits <= 9) return "Regular";
        return "Loyal";
    }

    // Analytics date range — mirrors ReportsController.ResolveRange so both
    // modules agree on what "This Year" etc. means.
    private static (DateTime from, DateTime to) ResolveAnalyticsRange(string range, DateTime today)
    {
        switch (range)
        {
            case "ThisWeek":
                {
                    int diff = (7 + (today.DayOfWeek - DayOfWeek.Monday)) % 7;
                    var start = today.AddDays(-diff).Date;
                    return (start, start.AddDays(6));
                }
            case "ThisMonth":
                return (new DateTime(today.Year, today.Month, 1),
                        new DateTime(today.Year, today.Month, 1).AddMonths(1).AddDays(-1));
            case "LastMonth":
                {
                    var first = new DateTime(today.Year, today.Month, 1).AddMonths(-1);
                    return (first, first.AddMonths(1).AddDays(-1));
                }
            case "LastYear":
                return (new DateTime(today.Year - 1, 1, 1), new DateTime(today.Year - 1, 12, 31));
            case "AllTime":
                return (new DateTime(2000, 1, 1), today);
            case "ThisYear":
            default:
                return (new DateTime(today.Year, 1, 1), new DateTime(today.Year, 12, 31));
        }
    }

    // =====================================================================
    // GET: api/analytics/platform
    // Platform-level Business Intelligence for Super Admin
    // Computes CRM SaaS metrics: tenants, subscriptions, billing, users, branches.
    // =====================================================================
    [HttpGet("platform")]
    public async Task<IActionResult> GetPlatformAnalytics()
    {
        try
        {
            // 1. Tenants
            var companies = await _db.Companies.AsNoTracking().ToListAsync();
            int totalTenants = companies.Count;
            int activeTenants = companies.Count(c => c.IsActive);
            int inactiveTenants = companies.Count(c => !c.IsActive);
            var thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);
            int newlyRegisteredTenants = companies.Count(c => c.CreatedAt >= thirtyDaysAgo);

            var tenantGrowth = companies
                .GroupBy(c => new { c.CreatedAt.Year, c.CreatedAt.Month })
                .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
                .Select(g => new
                {
                    period = new DateTime(g.Key.Year, g.Key.Month, 1).ToString("MMM yyyy"),
                    count = g.Count()
                })
                .ToList();

            // 2. Subscriptions
            var subscriptions = await _db.TenantSubscriptions
                .Include(s => s.Plan)
                .Include(s => s.Company)
                .AsNoTracking()
                .ToListAsync();

            int totalActiveSubscriptions = subscriptions.Count(s => s.Status == "Active");
            var now = DateTime.UtcNow;
            var next30Days = now.AddDays(30);

            int expiringSubscriptions = subscriptions.Count(s =>
                s.Status == "Active" &&
                s.RenewalDate.HasValue &&
                s.RenewalDate.Value >= now &&
                s.RenewalDate.Value <= next30Days);

            int expiredSubscriptions = subscriptions.Count(s =>
                s.Status == "Expired" ||
                (s.RenewalDate.HasValue && s.RenewalDate.Value < now && s.Status != "Active"));

            var plans = await _db.TenantSubscriptionPlans.AsNoTracking().ToListAsync();

            var subscriptionsByPlan = plans.Select(p => new
            {
                planId = p.PlanId,
                planName = p.PlanName,
                price = p.Price,
                priceFormatted = $"₱{p.Price:N2}",
                billingCycle = p.BillingCycle,
                count = subscriptions.Count(s => s.PlanId == p.PlanId && s.Status == "Active")
            }).ToList();

            var statusDistribution = subscriptions
                .GroupBy(s => string.IsNullOrWhiteSpace(s.Status) ? "Unknown" : s.Status)
                .Select(g => new
                {
                    status = g.Key,
                    count = g.Count()
                })
                .ToList();

            if (!statusDistribution.Any(s => s.status == "Active"))
                statusDistribution.Add(new { status = "Active", count = 0 });
            if (!statusDistribution.Any(s => s.status == "Expired"))
                statusDistribution.Add(new { status = "Expired", count = 0 });

            var subscriptionGrowth = subscriptions
                .Where(s => s.StartDate.HasValue)
                .GroupBy(s => new { s.StartDate!.Value.Year, s.StartDate.Value.Month })
                .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
                .Select(g => new
                {
                    period = new DateTime(g.Key.Year, g.Key.Month, 1).ToString("MMM yyyy"),
                    count = g.Count()
                })
                .ToList();

            // 3. Billing
            var transactions = await _db.TenantBillingTransactions
                .Include(t => t.Company)
                .AsNoTracking()
                .ToListAsync();

            decimal totalSubscriptionRevenue = transactions
                .Where(t => t.PaymentStatus == "Paid" || t.PaymentStatus == "Completed")
                .Sum(t => t.Amount);

            int paidBillingTransactions = transactions
                .Count(t => t.PaymentStatus == "Paid" || t.PaymentStatus == "Completed");

            decimal outstandingBillingAmount = transactions
                .Where(t => t.PaymentStatus == "Pending" || t.PaymentStatus == "Unpaid")
                .Sum(t => t.Amount);

            decimal overdueBillingAmount = transactions
                .Where(t => t.PaymentStatus == "Overdue" ||
                            (t.PaymentStatus != "Paid" && t.PaymentStatus != "Completed" && t.TransactionDate < DateTime.UtcNow.AddDays(-30)))
                .Sum(t => t.Amount);

            var revenueByMonth = transactions
                .Where(t => t.PaymentStatus == "Paid" || t.PaymentStatus == "Completed")
                .GroupBy(t => new { t.TransactionDate.Year, t.TransactionDate.Month })
                .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
                .Select(g => new
                {
                    month = new DateTime(g.Key.Year, g.Key.Month, 1).ToString("MMM yyyy"),
                    amount = g.Sum(x => x.Amount),
                    formatted = $"₱{g.Sum(x => x.Amount):N2}",
                    count = g.Count()
                })
                .ToList();

            var revenueByPlan = plans.Select(p =>
            {
                var matchingSubs = subscriptions.Where(s => s.PlanId == p.PlanId).Select(s => s.TenantSubscriptionId).ToHashSet();
                var matchingCompIds = subscriptions.Where(s => s.PlanId == p.PlanId).Select(s => s.CompanyId).ToHashSet();

                var planTxs = transactions.Where(t =>
                    (t.PaymentStatus == "Paid" || t.PaymentStatus == "Completed") &&
                    ((t.TenantSubscriptionId.HasValue && matchingSubs.Contains(t.TenantSubscriptionId.Value)) ||
                     matchingCompIds.Contains(t.CompanyId))).ToList();

                decimal planRevenue = planTxs.Sum(t => t.Amount);
                double pct = totalSubscriptionRevenue > 0 ? (double)(planRevenue / totalSubscriptionRevenue * 100m) : 0.0;

                return new
                {
                    planId = p.PlanId,
                    planName = p.PlanName,
                    amount = planRevenue,
                    formatted = $"₱{planRevenue:N2}",
                    percentage = Math.Round(pct, 1),
                    transactionCount = planTxs.Count
                };
            }).OrderByDescending(x => x.amount).ToList();

            // 4. Platform aggregates
            int totalRegisteredUsers = await _db.Users.CountAsync();

            int totalBranches = 0;
            foreach (var comp in companies)
            {
                try
                {
                    await using var tenant = await _tenantFactory.CreateAsync(comp.CompanyId);
                    totalBranches += await tenant.Branches.CountAsync(b => !b.IsArchived && b.IsActive);
                }
                catch
                {
                    totalBranches += 1;
                }
            }
            if (totalBranches < totalTenants) totalBranches = totalTenants;

            // 5. Recent Subscription Activity
            var recentActivity = transactions
                .OrderByDescending(t => t.TransactionDate)
                .ThenByDescending(t => t.TransactionId)
                .Take(10)
                .Select(t =>
                {
                    var comp = companies.FirstOrDefault(c => c.CompanyId == t.CompanyId);
                    var sub = subscriptions.FirstOrDefault(s => (t.TenantSubscriptionId.HasValue && s.TenantSubscriptionId == t.TenantSubscriptionId.Value) || s.CompanyId == t.CompanyId);
                    var planName = sub?.Plan?.PlanName ?? "Subscription";

                    return new
                    {
                        transactionId = t.TransactionId,
                        companyId = t.CompanyId,
                        companyName = comp?.CompanyName ?? $"Company #{t.CompanyId}",
                        companyCode = comp?.CompanyCode ?? $"COMP{t.CompanyId:D3}",
                        planName = planName,
                        amount = t.Amount,
                        amountFormatted = $"₱{t.Amount:N2}",
                        paymentMethod = t.PaymentMethod ?? "N/A",
                        paymentStatus = t.PaymentStatus,
                        date = t.TransactionDate,
                        dateFormatted = t.TransactionDate.ToString("MMM dd, yyyy")
                    };
                })
                .ToList();

            return Ok(new
            {
                platformOverview = new
                {
                    totalTenants,
                    activeTenants,
                    inactiveTenants,
                    totalActiveSubscriptions,
                    totalRevenue = totalSubscriptionRevenue,
                    totalRevenueFormatted = $"₱{totalSubscriptionRevenue:N2}",
                    totalRegisteredUsers,
                    totalBranches,
                    outstandingAmount = outstandingBillingAmount,
                    outstandingAmountFormatted = $"₱{outstandingBillingAmount:N2}"
                },
                tenantAnalytics = new
                {
                    totalTenants,
                    activeTenants,
                    inactiveTenants,
                    newlyRegisteredTenants,
                    tenantGrowth
                },
                subscriptionAnalytics = new
                {
                    totalActiveSubscriptions,
                    expiringSubscriptions,
                    expiredSubscriptions,
                    subscriptionsByPlan,
                    statusDistribution,
                    subscriptionGrowth
                },
                billingAnalytics = new
                {
                    totalSubscriptionRevenue,
                    totalSubscriptionRevenueFormatted = $"₱{totalSubscriptionRevenue:N2}",
                    paidBillingTransactions,
                    outstandingBillingAmount,
                    outstandingBillingAmountFormatted = $"₱{outstandingBillingAmount:N2}",
                    overdueBillingAmount,
                    overdueBillingAmountFormatted = $"₱{overdueBillingAmount:N2}",
                    revenueByMonth,
                    revenueByPlan
                },
                recentActivity
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Failed to calculate platform analytics.", error = ex.Message });
        }
    }
}