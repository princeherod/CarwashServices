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
        var customers = await tenant.TenantCustomers.AsNoTracking().ToListAsync();
        var products = await tenant.Products.AsNoTracking().ToListAsync();

        var custById = customers.ToDictionary(c => c.TenantCustomerId);
        var priceById = products.GroupBy(p => p.ProductId).ToDictionary(g => g.Key, g => g.First().UnitPrice);
        var nameById = products.GroupBy(p => p.ProductId).ToDictionary(g => g.Key, g => g.First().ProductName);

        var users = await _db.Users.AsNoTracking().ToListAsync();
        var userById = users.ToDictionary(u => u.UserId);

        // ---- KPIs ----
        int totalCustomers = customers.Count;

        var requests = await _db.ServiceRequests.AsNoTracking().ToListAsync();

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

        // ---- Recent Requests ----
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

        // ---- Follow-Up Queue ----
        var followUps = await _db.FollowUps.AsNoTracking().ToListAsync();

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

        // ---- Service Staff (only role 3) ----
        var staffList = users
            .Where(u => u.RoleId == 3)   // only actual staff
            .Select(u => new
            {
                userId = u.UserId,
                fullName = u.FullName,
                role = "Staff",
                isOnDuty = true
            })
            .ToList();

        // ---- Recent Status Logs ----
        var logs = await _db.ServiceStatusLogs
            .AsNoTracking()
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