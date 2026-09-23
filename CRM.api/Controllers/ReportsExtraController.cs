using CRM.Infrastructure.Data;
using CRM.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CRM.api.Controllers;

[ApiController]
[Route("api/reports")]
public class ReportsExtraController : ControllerBase
{
    private readonly MasterErpDbContext _db;
    private readonly ITenantDbContextFactory _tenantFactory;

    public ReportsExtraController(MasterErpDbContext db, ITenantDbContextFactory tenantFactory)
    {
        _db = db;
        _tenantFactory = tenantFactory;
    }

    // ================================================================
    //  COMPLAINTS & FEEDBACK
    // ================================================================
    [HttpGet("complaints-feedback")]
    public async Task<IActionResult> GetComplaintsFeedback(
        [FromQuery] int companyId = 1,
        [FromQuery] string range = "ThisYear")
    {
        var (from, to) = ResolveRange(range, DateTime.Today);

        var tenant = await _tenantFactory.CreateAsync(companyId);

        // Only active customers are counted.
        var customers = await tenant.TenantCustomers
            .AsNoTracking()
            .Where(c => !c.IsArchived)
            .ToListAsync();

        var custById = customers.ToDictionary(c => c.TenantCustomerId, c => c.CustomerName);

        var interactions = await tenant.CustomerInteractions.AsNoTracking()
            .Where(x => x.CreatedAt >= from && x.CreatedAt <= to)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();

        // Exclude interactions that belong to archived customers.
        var activeIds = custById.Keys.ToHashSet();
        interactions = interactions.Where(x => activeIds.Contains(x.CustomerId)).ToList();

        int totalFeedback = interactions.Count(x => x.Kind == "Feedback");
        int totalComplaints = interactions.Count(x => x.Kind == "Complaint");
        int resolvedComplaints = interactions.Count(x => x.Kind == "Complaint" && x.Status == "Resolved");
        double resolutionRate = totalComplaints == 0 ? 0 : Math.Round(resolvedComplaints * 100.0 / totalComplaints, 0);

        int positive = interactions.Count(x => x.Kind == "Feedback" && x.Status == "Resolved");
        int negative = interactions.Count(x => x.Kind == "Complaint" && x.Status == "Open");
        int neutral = interactions.Count - positive - negative;
        int neutralTotal = positive + negative + neutral;
        double avgRating = 0;

        var complaintsByCat = interactions
            .Where(x => x.Kind == "Complaint")
            .GroupBy(x => string.IsNullOrWhiteSpace(x.Title) ? "Other" : x.Title)
            .Select(g => new { label = g.Key, value = g.Count() })
            .OrderByDescending(x => x.value)
            .ToList();

        var rows = interactions.Select(x => new
        {
            type = x.Kind,
            date = x.CreatedAt.ToString("yyyy-MM-dd"),
            customer = custById.TryGetValue(x.CustomerId, out var n) ? n : $"id:{x.CustomerId}",
            details = x.Details ?? "",
            ratingOrCategory = x.Kind == "Feedback" ? (x.Severity ?? "—") : (x.Title ?? "—"),
            status = x.Status
        }).ToList();

        return Ok(new
        {
            totalFeedback,
            totalComplaints,
            resolvedComplaints,
            resolutionRate,
            avgRating,
            sentiment = new { positive, neutral, negative, total = neutralTotal },
            complaintsByCategory = complaintsByCat,
            rows,
            generatedAt = DateTime.Now.ToString("MMM d, yyyy, h:mm tt")
        });
    }

    // ================================================================
    //  CUSTOMER ACTIVITY
    // ================================================================
    [HttpGet("customer-activity")]
    public async Task<IActionResult> GetCustomerActivity(
        [FromQuery] int companyId = 1,
        [FromQuery] string range = "ThisYear")
    {
        var (from, to) = ResolveRange(range, DateTime.Today);

        var tenant = await _tenantFactory.CreateAsync(companyId);

        var customers = await tenant.TenantCustomers
            .AsNoTracking()
            .Where(c => !c.IsArchived)
            .ToListAsync();

        var products = await tenant.Products
            .AsNoTracking()
            .Where(p => !p.IsArchived)
            .ToListAsync();

        var priceById = products.GroupBy(p => p.ProductId).ToDictionary(g => g.Key, g => g.First().UnitPrice);

        var requests = await _db.ServiceRequests
            .AsNoTracking()
            .Where(r => !r.IsArchived
                     && r.RequestedDate >= from
                     && r.RequestedDate <= to)
            .ToListAsync();

        var byCustomer = requests.GroupBy(r => r.CustomerId).ToDictionary(g => g.Key, g => g.ToList());

        var rows = customers.Select(c =>
        {
            byCustomer.TryGetValue(c.TenantCustomerId, out var visits);
            visits ??= new();

            int totalVisits = visits.Count;
            DateTime? lastVisit = visits.Count == 0 ? null : visits.Max(v => v.RequestedDate);
            decimal lifetime = visits.Where(v => v.Status == "Completed")
                                     .Sum(v => priceById.TryGetValue(v.ServiceId, out var p) ? p : 0m);
            decimal avgSpend = totalVisits == 0 ? 0 : Math.Round(lifetime / totalVisits, 2);
            string status = totalVisits == 0 ? "Never visited" : "Active";

            return new
            {
                customer = c.CustomerName,
                phone = c.ContactNumber ?? "",
                vehicleType = c.VehicleType ?? "",
                source = c.Source ?? "",
                totalVisits,
                lastVisit = lastVisit?.ToString("yyyy-MM-dd") ?? "—",
                lifetimeValue = lifetime,
                avgSpend,
                status
            };
        }).OrderByDescending(r => r.totalVisits).ToList();

        int totalCustomers = customers.Count;
        int activeCustomers = customers.Count(c =>
        {
            byCustomer.TryGetValue(c.TenantCustomerId, out var visits);
            return visits != null && visits.Any(v => (DateTime.Today - v.RequestedDate).TotalDays <= 60);
        });
        int returningCustomers = byCustomer.Count(kv => kv.Value.Count > 1);
        double returningRate = totalCustomers == 0 ? 0 : Math.Round(returningCustomers * 100.0 / totalCustomers, 0);
        double avgServicesPerCustomer = totalCustomers == 0 ? 0 : Math.Round(requests.Count * 1.0 / totalCustomers, 1);

        var bySource = customers
            .Where(c => !string.IsNullOrWhiteSpace(c.Source))
            .GroupBy(c => c.Source!)
            .Select(g => new { label = g.Key, value = g.Count() })
            .OrderByDescending(x => x.value)
            .ToList();

        var byVehicle = customers
            .Where(c => !string.IsNullOrWhiteSpace(c.VehicleType))
            .GroupBy(c => c.VehicleType!)
            .Select(g => new { label = g.Key, value = g.Count() })
            .OrderByDescending(x => x.value)
            .ToList();

        return Ok(new
        {
            totalCustomers,
            activeCustomers,
            returningRate,
            avgServicesPerCustomer,
            bySource,
            byVehicle,
            rows,
            generatedAt = DateTime.Now.ToString("MMM d, yyyy, h:mm tt")
        });
    }

    // ================================================================
    //  RETENTION SUMMARY
    // ================================================================
    [HttpGet("retention-summary")]
    public async Task<IActionResult> GetRetentionSummary(
        [FromQuery] int companyId = 1,
        [FromQuery] string range = "ThisYear")
    {
        var (from, to) = ResolveRange(range, DateTime.Today);

        var tenant = await _tenantFactory.CreateAsync(companyId);

        var customers = await tenant.TenantCustomers
            .AsNoTracking()
            .Where(c => !c.IsArchived)
            .ToListAsync();

        var products = await tenant.Products
            .AsNoTracking()
            .Where(p => !p.IsArchived)
            .ToListAsync();

        var priceById = products.GroupBy(p => p.ProductId).ToDictionary(g => g.Key, g => g.First().UnitPrice);

        var requests = await _db.ServiceRequests
            .AsNoTracking()
            .Where(r => !r.IsArchived)
            .ToListAsync();

        var interactions = await tenant.CustomerInteractions
            .AsNoTracking()
            .ToListAsync();

        // Exclude interactions that belong to archived customers.
        var activeIds = customers.Select(c => c.TenantCustomerId).ToHashSet();
        interactions = interactions.Where(x => activeIds.Contains(x.CustomerId)).ToList();

        var byCustomer = requests.GroupBy(r => r.CustomerId).ToDictionary(g => g.Key, g => g.ToList());
        var complaintsByCustomer = interactions
            .Where(x => x.Kind == "Complaint" && x.Status == "Open")
            .GroupBy(x => x.CustomerId)
            .ToDictionary(g => g.Key, g => g.Count());

        var today = DateTime.Today;
        var rows = customers.Select(c =>
        {
            byCustomer.TryGetValue(c.TenantCustomerId, out var visits);
            visits ??= new();

            DateTime? lastVisit = visits.Count == 0 ? null : visits.Max(v => v.RequestedDate);
            int daysSince = lastVisit.HasValue ? (today - lastVisit.Value.Date).Days : 9999;
            int totalVisits = visits.Count;
            decimal ltv = visits.Where(v => v.Status == "Completed")
                                .Sum(v => priceById.TryGetValue(v.ServiceId, out var p) ? p : 0m);

            string segment =
                daysSince <= 60 ? "Healthy"
                : daysSince <= 120 ? "At Risk"
                : "Churned";

            string action = segment switch
            {
                "At Risk" => "Send follow-up",
                "Churned" => "Win-back campaign",
                _ => "Maintain"
            };

            string complaint = complaintsByCustomer.TryGetValue(c.TenantCustomerId, out var cc) && cc > 0
                ? "Unresolved"
                : "None";

            return new
            {
                customer = c.CustomerName,
                lastVisit = lastVisit?.ToString("yyyy-MM-dd") ?? "—",
                daysSince = daysSince >= 9999 ? "—" : daysSince.ToString(),
                totalVisits,
                ltv,
                avgRating = "—",
                complaint,
                segment,
                retentionAction = action
            };
        }).ToList();

        int totalCustomers = customers.Count;
        int healthy = rows.Count(r => r.segment == "Healthy");
        int atRisk = rows.Count(r => r.segment == "At Risk");
        int churned = rows.Count(r => r.segment == "Churned");
        int unresolved = interactions.Count(x => x.Kind == "Complaint" && x.Status == "Open");
        double retentionRate = totalCustomers == 0 ? 0 : Math.Round(healthy * 100.0 / totalCustomers, 0);

        var topByLtv = rows.OrderByDescending(r => r.ltv).Take(10)
            .Select(r => new { label = r.customer, value = r.ltv }).ToList();

        return Ok(new
        {
            retentionRate,
            atRisk,
            churned,
            unresolved,
            segmentation = new
            {
                healthy,
                atRisk,
                churned,
                total = totalCustomers
            },
            topByLtv,
            rows,
            generatedAt = DateTime.Now.ToString("MMM d, yyyy, h:mm tt")
        });
    }

    // ================================================================
    private static (DateTime from, DateTime to) ResolveRange(string range, DateTime today)
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
            case "ThisYear":
                return (new DateTime(today.Year, 1, 1), new DateTime(today.Year, 12, 31));
            case "LastYear":
                return (new DateTime(today.Year - 1, 1, 1), new DateTime(today.Year - 1, 12, 31));
            default:
                return (new DateTime(2000, 1, 1), today);
        }
    }
}