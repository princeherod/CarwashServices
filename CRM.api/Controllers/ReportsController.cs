using CRM.Infrastructure.Data;
using CRM.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CRM.api.Controllers;

[ApiController]
[Route("api/reports")]
public class ReportsController : ControllerBase
{
    private readonly MasterErpDbContext _db;
    private readonly ITenantDbContextFactory _tenantFactory;

    public ReportsController(MasterErpDbContext db, ITenantDbContextFactory tenantFactory)
    {
        _db = db;
        _tenantFactory = tenantFactory;
    }

    // GET: api/reports/service-revenue?companyId=1&range=ThisYear&service=All&vehicle=All
    [HttpGet("service-revenue")]
    public async Task<IActionResult> GetServiceRevenueReport(
        [FromQuery] int companyId = 1,
        [FromQuery] string range = "ThisYear",
        [FromQuery] string service = "All",
        [FromQuery] string vehicle = "All")
    {
        var today = DateTime.Today;
        var (from, to) = ResolveRange(range, today);

        var tenant = await _tenantFactory.CreateAsync(companyId);

        // Only active customers and active services.
        var customers = await tenant.TenantCustomers
            .AsNoTracking()
            .Where(c => !c.IsArchived)
            .ToListAsync();

        var products = await tenant.Products
            .AsNoTracking()
            .Where(p => !p.IsArchived)
            .ToListAsync();

        var custById = customers.ToDictionary(c => c.TenantCustomerId);
        var priceById = products.GroupBy(p => p.ProductId)
                                .ToDictionary(g => g.Key, g => g.First().UnitPrice);
        var nameById = products.GroupBy(p => p.ProductId)
                               .ToDictionary(g => g.Key, g => g.First().ProductName);

        // Only active requests within the range.
        var requests = await tenant.ServiceRequests
            .AsNoTracking()
            .Where(r => !r.IsArchived
                     && r.RequestedDate >= from
                     && r.RequestedDate <= to)
            .ToListAsync();

        if (service != "All")
        {
            var productIds = nameById.Where(kv => kv.Value == service).Select(kv => kv.Key).ToHashSet();
            requests = requests.Where(r => productIds.Contains(r.ServiceId)).ToList();
        }

        if (vehicle != "All")
        {
            var custIds = customers.Where(c => c.VehicleType == vehicle)
                                   .Select(c => c.TenantCustomerId).ToHashSet();
            requests = requests.Where(r => custIds.Contains(r.CustomerId)).ToList();
        }

        // KPIs
        int totalTransactions = requests.Count;
        int completed = requests.Count(r => r.Status == "Completed");
        int pendingOrCancelled = requests.Count(r => r.Status == "Pending"
                                                  || r.Status == "Cancelled"
                                                  || r.Status == "InProgress");

        decimal totalRevenue = 0m;
        foreach (var r in requests.Where(r => r.Status == "Completed"))
            if (priceById.TryGetValue(r.ServiceId, out var p)) totalRevenue += p;

        decimal avgTicket = completed > 0 ? totalRevenue / completed : 0m;

        // Revenue by month
        var months = new List<object>();
        var cursor = new DateTime(from.Year, from.Month, 1);
        var last = new DateTime(to.Year, to.Month, 1);
        while (cursor <= last)
        {
            var mStart = cursor;
            var mEnd = cursor.AddMonths(1).AddDays(-1);

            decimal sum = 0m;
            foreach (var r in requests.Where(r => r.Status == "Completed"
                                              && r.RequestedDate >= mStart
                                              && r.RequestedDate <= mEnd))
                if (priceById.TryGetValue(r.ServiceId, out var p)) sum += p;

            months.Add(new { label = mStart.ToString("MM ''yy"), value = sum });
            cursor = cursor.AddMonths(1);
        }

        // Revenue by service
        var byService = new List<object>();
        foreach (var g in requests.Where(r => r.Status == "Completed").GroupBy(r => r.ServiceId))
        {
            decimal sum = 0m;
            if (priceById.TryGetValue(g.Key, out var p)) sum = p * g.Count();
            byService.Add(new
            {
                label = nameById.TryGetValue(g.Key, out var n) ? n : $"Service {g.Key}",
                value = sum
            });
        }
        byService = byService.OrderByDescending(x => ((dynamic)x).value).Cast<object>().ToList();

        // Transactions (top 200)
        // Transactions (top 200) — includes the raw tenant customer id
        var txns = requests
            .OrderByDescending(r => r.RequestedDate)
            .Take(200)
            .Select(r =>
            {
                var c = custById.TryGetValue(r.CustomerId, out var cc) ? cc : null;
                string sname = nameById.TryGetValue(r.ServiceId, out var nn) ? nn : $"Service {r.ServiceId}";
                decimal amount = priceById.TryGetValue(r.ServiceId, out var pp) ? pp : 0m;

                string payment = r.Status switch
                {
                    "Completed" => "Paid",
                    "Pending" => "Pending",
                    "Cancelled" => "Cancelled",
                    "InProgress" => "Partial",
                    _ => "Pending"
                };

                return new
                {
                    txn = $"#{r.RequestId}",
                    date = r.RequestedDate.ToString("yyyy-MM-dd"),
                    customerId = r.CustomerId,               // NEW
                    customer = c?.CustomerName ?? $"id:{r.CustomerId}",
                    vehicle = string.IsNullOrWhiteSpace(c?.VehicleType) ? "—" : c.VehicleType,
                    service = sname,
                    amount,
                    payment,
                    status = r.Status
                };
            })
            .ToList();

        // Dropdown options
        var serviceOptions = new List<string> { "All" };
        serviceOptions.AddRange(products.OrderBy(p => p.ProductName).Select(p => p.ProductName));

        var vehicleOptions = new List<string> { "All" };
        vehicleOptions.AddRange(customers
            .Where(c => !string.IsNullOrWhiteSpace(c.VehicleType))
            .Select(c => c.VehicleType!)
            .Distinct()
            .OrderBy(v => v));

        return Ok(new
        {
            totalTransactions,
            completed,
            pendingOrCancelled,
            totalRevenue,
            avgTicket,
            months,
            byService,
            transactions = txns,
            serviceOptions,
            vehicleOptions,
            generatedAt = DateTime.Now.ToString("MMM d, yyyy, h:mm tt")
        });
    }

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