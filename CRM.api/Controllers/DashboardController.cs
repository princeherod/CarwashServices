using CRM.Infrastructure.Data;
using CRM.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CRM.api.Controllers;

[ApiController]
[Route("api/dashboard")]
public class DashboardController : ControllerBase
{
    private readonly MasterErpDbContext _db;
    private readonly ITenantDbContextFactory _tenantFactory;

    public DashboardController(MasterErpDbContext db, ITenantDbContextFactory tenantFactory)
    {
        _db = db;
        _tenantFactory = tenantFactory;
    }

    [HttpGet]
    public async Task<IActionResult> GetDashboard([FromQuery] int companyId = 1)
    {
        var today = DateTime.Today;
        var monthStart = new DateTime(today.Year, today.Month, 1);

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
        var priceById = products.GroupBy(p => p.ProductId).ToDictionary(g => g.Key, g => g.First().UnitPrice);
        var nameById = products.GroupBy(p => p.ProductId).ToDictionary(g => g.Key, g => g.First().ProductName);

        var users = await _db.Users.AsNoTracking().ToListAsync();
        var userById = users.ToDictionary(u => u.UserId);

        // Only active service requests.
        var requests = await _db.ServiceRequests
            .AsNoTracking()
            .Where(r => !r.IsArchived)
            .ToListAsync();

        // KPIs
        int totalCustomers = customers.Count;

        int todayJobs = requests.Count(r =>
            r.ScheduledDate.HasValue && r.ScheduledDate.Value.Date == today);

        int inProgress = requests.Count(r => r.Status == "InProgress" || r.Status == "Assigned");
        int pending = requests.Count(r => r.Status == "Pending");

        decimal revenueThisMonth = 0m;
        foreach (var r in requests.Where(r => r.Status == "Completed"
                                          && r.CompletedDate.HasValue
                                          && r.CompletedDate.Value >= monthStart))
        {
            if (priceById.TryGetValue(r.ServiceId, out var p))
                revenueThisMonth += p;
        }

        // Recent Requests (top 5)
        var recentRequests = requests
            .OrderByDescending(r => r.RequestId)
            .Take(5)
            .Select(r =>
            {
                var c = custById.TryGetValue(r.CustomerId, out var cc) ? cc : null;
                var s = nameById.TryGetValue(r.ServiceId, out var nn) ? nn : $"Service {r.ServiceId}";
                var staff = r.AssignedStaffId.HasValue && userById.TryGetValue(r.AssignedStaffId.Value, out var uu)
                            ? uu.FullName : "Unassigned";

                return new
                {
                    requestId = r.RequestId,
                    customer = c?.CustomerName ?? $"id:{r.CustomerId}",
                    plate = c?.PlateNumber ?? "",
                    service = s,
                    scheduledDate = r.ScheduledDate?.ToString("yyyy-MM-dd") ?? "",
                    assignedStaff = staff,
                    status = r.Status
                };
            })
            .ToList();

        // Follow-Up Queue — only non-archived follow-ups.
        var followUps = await _db.FollowUps
            .AsNoTracking()
            .Where(f => !f.IsArchived)
            .ToListAsync();

        var queue = followUps
            .Where(f => f.Status == "Pending" || f.Status == "Scheduled" || f.Status == "Due today")
            .OrderBy(f => f.ScheduledDate)
            .Take(5)
            .Select(f =>
            {
                var c = custById.TryGetValue(f.CustomerId, out var cc) ? cc : null;
                return new
                {
                    followUpId = f.FollowUpId,
                    customer = c?.CustomerName ?? $"id:{f.CustomerId}",
                    type = f.Type,
                    scheduledDate = f.ScheduledDate.ToString("yyyy-MM-dd"),
                    status = f.Status
                };
            })
            .ToList();

        int followUpPending = followUps.Count(f => f.Status == "Pending" || f.Status == "Scheduled");

        // Service Staff (only role 3)
        var staffList = users
            .Where(u => u.RoleId == 3)
            .Select(u => new
            {
                userId = u.UserId,
                fullName = u.FullName,
                role = "Staff",
                isOnDuty = true
            })
            .ToList();

        // Recent Status Logs — only logs whose request is still active.
        var activeRequestIds = requests.Select(r => r.RequestId).ToHashSet();

        var logs = await _db.ServiceStatusLogs
            .AsNoTracking()
            .Where(l => activeRequestIds.Contains(l.RequestId))
            .OrderByDescending(l => l.LogId)
            .Take(5)
            .ToListAsync();

        var recentLogs = logs.Select(l =>
        {
            var u = userById.TryGetValue(l.UpdatedBy, out var uu) ? uu : null;
            return new
            {
                logId = l.LogId,
                requestId = l.RequestId,
                status = l.Status,
                updatedBy = u?.FullName ?? $"id:{l.UpdatedBy}",
                updatedAt = l.UpdatedAt.ToString("yyyy-MM-dd HH:mm:ss"),
                notes = l.Notes ?? ""
            };
        }).ToList();

        return Ok(new
        {
            totalCustomers,
            todayJobs,
            inProgress,
            pending,
            revenueThisMonth,
            recentRequests,
            followUpQueue = queue,
            followUpPendingCount = followUpPending,
            serviceStaff = staffList,
            recentLogs
        });
    }
}