using CRM.Infrastructure.Data;
using CRM.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CRM.api.Controllers;

/// <summary>
/// Service Staff dashboard — read-only, scoped to a single staff user.
///
/// Every query filters by AssignedStaffId = staffId, so a Service Staff
/// member never sees work that belongs to another staff member.
///
/// This controller does NOT modify DashboardController. It's an
/// additional endpoint used only by the Service Staff dashboard.
/// </summary>
[ApiController]
[Route("api/dashboard/staff")]
public class StaffDashboardController : ControllerBase
{
    private readonly MasterErpDbContext _db;
    private readonly ITenantDbContextFactory _tenantFactory;

    public StaffDashboardController(
        MasterErpDbContext db,
        ITenantDbContextFactory tenantFactory)
    {
        _db = db;
        _tenantFactory = tenantFactory;
    }

    // GET: api/dashboard/staff?companyId=1&staffId=15
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] int companyId = 1,
                                          [FromQuery] int staffId = 0)
    {
        if (staffId <= 0)
            return BadRequest(new { message = "staffId is required." });

        var today = DateTime.Today;
        var monthStart = new DateTime(today.Year, today.Month, 1);

        // ---- Tenant lookups (customers + services) ----
        var tenant = await _tenantFactory.CreateAsync(companyId);

        var customers = await tenant.TenantCustomers
            .AsNoTracking()
            .Where(c => !c.IsArchived)
            .ToListAsync();

        var products = await tenant.Products
            .AsNoTracking()
            .Where(p => !p.IsArchived)
            .ToListAsync();

        var custById = customers.ToDictionary(c => c.TenantCustomerId);
        var prodById = products.GroupBy(p => p.ProductId)
                               .ToDictionary(g => g.Key, g => g.First());

        // ---- Master lookups (users for status-log author) ----
        var users = await _db.Users.AsNoTracking().ToListAsync();
        var userById = users.ToDictionary(u => u.UserId);

        // ---- Every request assigned to this staff member ----
        var myRequests = await _db.ServiceRequests
            .AsNoTracking()
            .Where(r => !r.IsArchived && r.AssignedStaffId == staffId)
            .OrderByDescending(r => r.RequestId)
            .ToListAsync();

        // ---- KPI counts ----
        int assignedRequests = myRequests.Count;

        int inProgress = myRequests.Count(r =>
            string.Equals(r.Status, "InProgress", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(r.Status, "Assigned", StringComparison.OrdinalIgnoreCase));

        int completed = myRequests.Count(r =>
            string.Equals(r.Status, "Completed", StringComparison.OrdinalIgnoreCase));

        // ---- Today's assigned services ----
        var todaysRequests = myRequests
            .Where(r => r.ScheduledDate.HasValue
                     && r.ScheduledDate.Value.Date == today)
            .OrderBy(r => r.ScheduledDate)
            .Select(r => BuildRequestRow(r, custById, prodById))
            .ToList();

        // ---- Pending / upcoming requests (not Completed / Cancelled) ----
        var pendingRequests = myRequests
            .Where(r => !string.Equals(r.Status, "Completed", StringComparison.OrdinalIgnoreCase)
                     && !string.Equals(r.Status, "Cancelled", StringComparison.OrdinalIgnoreCase))
            .OrderBy(r => r.ScheduledDate ?? r.RequestedDate)
            .Take(20)
            .Select(r => BuildRequestRow(r, custById, prodById))
            .ToList();

        // ---- My customers (distinct customers tied to my assigned requests) ----
        var myCustomers = myRequests
            .Where(r => r.CustomerId > 0)
            .GroupBy(r => r.CustomerId)
            .Select(g =>
            {
                var latest = g.OrderByDescending(r => r.RequestId).First();
                var cust = custById.TryGetValue(latest.CustomerId, out var c) ? c : null;
                var svc = prodById.TryGetValue(latest.ServiceId, out var p) ? p : null;

                return new
                {
                    customerId = latest.CustomerId,
                    customer = cust?.CustomerName ?? $"id:{latest.CustomerId}",
                    plate = cust?.PlateNumber ?? "",
                    service = svc?.ProductName ?? $"Service {latest.ServiceId}",
                    scheduledDate = latest.ScheduledDate?.ToString("yyyy-MM-dd HH:mm") ?? "",
                    status = latest.Status ?? "Pending"
                };
            })
            .OrderByDescending(x => x.scheduledDate)
            .ToList();

        // ---- Recent service activity by this staff member ----
        // Logs are linked to requests; we only keep logs whose request is
        // currently assigned to this staff member.
        var myRequestIds = myRequests.Select(r => r.RequestId).ToHashSet();

        var recentLogsRaw = await _db.ServiceStatusLogs
            .AsNoTracking()
            .Where(l => myRequestIds.Contains(l.RequestId))
            .OrderByDescending(l => l.LogId)
            .Take(10)
            .ToListAsync();

        var requestById = myRequests.ToDictionary(r => r.RequestId);

        var recentActivity = recentLogsRaw.Select(l =>
        {
            var req = requestById.TryGetValue(l.RequestId, out var r) ? r : null;
            var cust = req != null && custById.TryGetValue(req.CustomerId, out var c) ? c : null;
            var svc = req != null && prodById.TryGetValue(req.ServiceId, out var p) ? p : null;
            var who = userById.TryGetValue(l.UpdatedBy, out var u) ? u.FullName : $"id:{l.UpdatedBy}";

            return new
            {
                logId = l.LogId,
                requestId = l.RequestId,
                customer = cust?.CustomerName ?? "",
                service = svc?.ProductName ?? "",
                status = l.Status ?? "",
                updatedBy = who,
                updatedAt = l.UpdatedAt.ToString("yyyy-MM-dd HH:mm:ss"),
                notes = l.Notes ?? ""
            };
        }).ToList();

        // ---- Follow-up approval status ----
        // The current FollowUp schema does not have an approval column, so
        // we only count what we can derive from status:
        //   Pending   → Status = "Pending" or "Scheduled"
        //   Approved  → Status = "Sent", "Contacted", or "Redeemed"
        //   Rejected  → Status = "Expired" (best-effort approximation)
        //
        // When the Admin approval module is built later, this block will
        // be replaced with a direct read of the new column. Nothing else
        // in the dashboard depends on those numbers.
        var myCustomersIds = myRequests.Select(r => r.CustomerId).Distinct().ToHashSet();

        var myFollowUps = await _db.FollowUps
            .AsNoTracking()
            .Where(f => !f.IsArchived && myCustomersIds.Contains(f.CustomerId))
            .ToListAsync();

        int fuPending = myFollowUps.Count(f =>
            string.Equals(f.Status, "Pending", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(f.Status, "Scheduled", StringComparison.OrdinalIgnoreCase));

        int fuApproved = myFollowUps.Count(f =>
            string.Equals(f.Status, "Sent", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(f.Status, "Contacted", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(f.Status, "Redeemed", StringComparison.OrdinalIgnoreCase));

        int fuRejected = myFollowUps.Count(f =>
            string.Equals(f.Status, "Expired", StringComparison.OrdinalIgnoreCase));

        return Ok(new
        {
            staffId,
            assignedRequests,
            inProgress,
            completed,
            pendingRequests,
            todaysRequests,
            myCustomers,
            recentActivity,
            followUpApproval = new
            {
                pending = fuPending,
                approved = fuApproved,
                rejected = fuRejected
            },
            generatedAt = DateTime.Now.ToString("MMM d, yyyy, h:mm tt")
        });
    }

    // ================================================================
    //  Helper — one row of request info used by multiple panels
    // ================================================================
    private static object BuildRequestRow(
        CRM.domain.Entities.ServiceRequest r,
        Dictionary<int, CRM.Domain.Entities.TenantCustomer> custById,
        Dictionary<int, CRM.Domain.Entities.Product> prodById)
    {
        var cust = custById.TryGetValue(r.CustomerId, out var c) ? c : null;
        var svc = prodById.TryGetValue(r.ServiceId, out var p) ? p : null;

        return new
        {
            requestId = r.RequestId,
            customerId = r.CustomerId,
            customer = cust?.CustomerName ?? $"id:{r.CustomerId}",
            plate = cust?.PlateNumber ?? "",
            serviceId = r.ServiceId,
            service = svc?.ProductName ?? $"Service {r.ServiceId}",
            servicePrice = svc?.UnitPrice ?? 0m,
            scheduledDate = r.ScheduledDate?.ToString("yyyy-MM-dd HH:mm") ?? "",
            completedDate = r.CompletedDate?.ToString("yyyy-MM-dd HH:mm") ?? "",
            priority = r.Priority ?? "Normal",
            status = r.Status ?? "Pending",
            notes = r.Notes ?? ""
        };
    }
}