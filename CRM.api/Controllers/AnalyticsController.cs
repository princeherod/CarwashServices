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
    public async Task<IActionResult> GetSummary([FromQuery] int companyId = 1)
    {
        var today = DateTime.Today;
        var monthStart = new DateTime(today.Year, today.Month, 1);

        var tenant = await _tenantFactory.CreateAsync(companyId);
        var customers = await tenant.TenantCustomers.AsNoTracking().ToListAsync();
        var totalCustomers = customers.Count;

        var requests = await _db.ServiceRequests.AsNoTracking().ToListAsync();

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

        var tenantProducts = await tenant.Products.AsNoTracking().ToListAsync();
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
        var customers = await tenant.TenantCustomers.AsNoTracking().ToListAsync();
        var requests = await _db.ServiceRequests.AsNoTracking().ToListAsync();

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
        var customers = await tenant.TenantCustomers.AsNoTracking().ToListAsync();
        var requests = await _db.ServiceRequests.AsNoTracking().ToListAsync();

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

    // GET: api/analytics/segment-customers?companyId=1&segment=AtRisk
    [HttpGet("segment-customers")]
    public async Task<IActionResult> GetSegmentCustomers(
        [FromQuery] int companyId = 1,
        [FromQuery] string segment = "AtRisk")
    {
        var tenant = await _tenantFactory.CreateAsync(companyId);
        var customers = await tenant.TenantCustomers.AsNoTracking().ToListAsync();
        var requests = await _db.ServiceRequests.AsNoTracking().ToListAsync();

        var rows = SegmentCustomers(customers, requests)
            .Where(s => s.Segment == segment)
            .ToList();

        return Ok(rows);
    }

    // GET: api/analytics/revenue?companyId=1
    [HttpGet("revenue")]
    public async Task<IActionResult> GetRevenue([FromQuery] int companyId = 1)
    {
        var today = DateTime.Today;
        var tenant = await _tenantFactory.CreateAsync(companyId);
        var products = await tenant.Products.AsNoTracking().ToListAsync();
        var priceLookup = products.GroupBy(p => p.ProductId)
            .ToDictionary(g => g.Key, g => g.First().UnitPrice);

        var requests = await _db.ServiceRequests.AsNoTracking()
            .Where(r => r.Status == "Completed" && r.CompletedDate != null)
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
    public async Task<IActionResult> GetRecent()
    {
        var list = await _db.ServiceRequests
            .AsNoTracking()
            .OrderByDescending(r => r.RequestId)
            .Take(8)
            .ToListAsync();

        return Ok(list);
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
    }

    // NOTE the two different namespace casings below:
    //   - TenantCustomer lives in  CRM.Domain.Entities  (capital D)
    //   - ServiceRequest lives in  CRM.domain.Entities  (lowercase d)
    // Match whatever each entity file actually declares.
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
}