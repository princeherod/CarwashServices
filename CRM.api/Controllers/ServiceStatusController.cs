using CRM.Infrastructure.Data;
using CRM.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CRM.api.Controllers;

[ApiController]
[Route("api/service-status")]
public class ServiceStatusController : ControllerBase
{
    private readonly MasterErpDbContext _db;
    private readonly ITenantDbContextFactory _tenantFactory;

    public ServiceStatusController(MasterErpDbContext db, ITenantDbContextFactory tenantFactory)
    {
        _db = db;
        _tenantFactory = tenantFactory;
    }

    // GET: api/service-status?companyId=1&branchId=2
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] int companyId = 1, [FromQuery] int? branchId = null)
    {
        var sec = await CRM.api.Services.BranchSecurityHelper.ResolveAndValidateAsync(_db, HttpContext, companyId, branchId);
        if (!sec.Allowed) return StatusCode(403, new { message = sec.ErrorMessage });
        branchId = sec.EffectiveBranchId;

        var tenant = await _tenantFactory.CreateAsync(companyId);

        // Load everything that's needed up front, then do the joins in memory.
        // This keeps the round-trip count low, which is the main reason the
        // previous version was timing out.
        var custQuery = tenant.TenantCustomers
            .AsNoTracking()
            .Where(c => !c.IsArchived);

        if (branchId.HasValue && branchId.Value > 0)
        {
            custQuery = custQuery.Where(c => c.BranchId == branchId.Value);
        }

        var customers = await custQuery.ToListAsync();

        var prodQuery = tenant.Products
            .AsNoTracking()
            .Where(p => !p.IsArchived);

        if (branchId.HasValue && branchId.Value > 0)
        {
            prodQuery = prodQuery.Where(p => p.BranchId == branchId.Value);
        }

        var products = await prodQuery.ToListAsync();

        var users = await _db.Users
            .AsNoTracking()
            .ToListAsync();

        var reqQuery = tenant.ServiceRequests
            .AsNoTracking()
            .Where(r => !r.IsArchived);

        if (branchId.HasValue && branchId.Value > 0)
        {
            reqQuery = reqQuery.Where(r => r.BranchId == branchId.Value);
        }

        var requests = await reqQuery
            .OrderByDescending(r => r.RequestId)
            .ToListAsync();

        var requestIds = requests.Select(r => r.RequestId).ToHashSet();

        // One query for all logs — used by BOTH panels.
        var allLogs = await tenant.ServiceStatusLogs
            .AsNoTracking()
            .Where(l => requestIds.Contains(l.RequestId))
            .OrderByDescending(l => l.LogId)
            .ToListAsync();

        var custById = customers.ToDictionary(c => c.TenantCustomerId);
        var prodById = products.GroupBy(p => p.ProductId)
                               .ToDictionary(g => g.Key, g => g.First());
        var userById = users.ToDictionary(u => u.UserId);

        // Latest log per request, derived from the single query above.
        var latestByRequest = allLogs
            .GroupBy(l => l.RequestId)
            .ToDictionary(g => g.Key, g => g.First()); // list is newest-first already

        // ---- Panel 1: one row per active ServiceRequest ----
        var current = new List<object>();
        int pending = 0, inProgress = 0, completed = 0, cancelled = 0;

        foreach (var r in requests)
        {
            switch ((r.Status ?? "").Trim().ToLowerInvariant())
            {
                case "pending": pending++; break;
                case "assigned":
                case "inprogress": inProgress++; break;
                case "completed": completed++; break;
                case "cancelled": cancelled++; break;
            }

            var cust = custById.TryGetValue(r.CustomerId, out var c) ? c : null;
            var svc = prodById.TryGetValue(r.ServiceId, out var p) ? p : null;

            string staff = "Unassigned";
            if (r.AssignedStaffId.HasValue &&
                userById.TryGetValue(r.AssignedStaffId.Value, out var st))
            {
                staff = st.FullName;
            }

            string lastUpdated = "";
            string updatedBy = "";
            if (latestByRequest.TryGetValue(r.RequestId, out var log))
            {
                lastUpdated = log.UpdatedAt.ToString("yyyy-MM-dd HH:mm");
                if (userById.TryGetValue(log.UpdatedBy, out var lu))
                    updatedBy = lu.FullName;
            }

            current.Add(new
            {
                requestId = r.RequestId,
                customerId = r.CustomerId,
                customer = cust?.CustomerName ?? $"id:{r.CustomerId}",
                plate = cust?.PlateNumber ?? "",
                serviceId = r.ServiceId,
                service = svc?.ProductName ?? $"id:{r.ServiceId}",
                servicePrice = svc?.UnitPrice ?? 0m,
                assignedStaff = staff,
                assignedStaffId = r.AssignedStaffId,
                status = r.Status ?? "Pending",
                priority = r.Priority ?? "Normal",
                requestedDate = r.RequestedDate.ToString("yyyy-MM-dd HH:mm"),
                scheduledDate = r.ScheduledDate?.ToString("yyyy-MM-dd HH:mm") ?? "",
                completedDate = r.CompletedDate?.ToString("yyyy-MM-dd HH:mm") ?? "",
                lastUpdated,
                updatedBy
            });
        }

        // ---- Panel 2: every status-change log, newest first ----
        var requestById = requests.ToDictionary(r => r.RequestId);

        var history = allLogs.Select(l =>
        {
            var req = requestById.TryGetValue(l.RequestId, out var rr) ? rr : null;
            var cust = req != null && custById.TryGetValue(req.CustomerId, out var c) ? c : null;
            var svc = req != null && prodById.TryGetValue(req.ServiceId, out var p) ? p : null;
            string who = userById.TryGetValue(l.UpdatedBy, out var u) ? u.FullName : $"id:{l.UpdatedBy}";

            return new
            {
                logId = l.LogId,
                requestId = l.RequestId,
                customer = cust?.CustomerName ?? "",
                service = svc?.ProductName ?? "",
                status = l.Status ?? "",
                updatedAt = l.UpdatedAt.ToString("yyyy-MM-dd HH:mm:ss"),
                updatedBy = who,
                notes = l.Notes ?? ""
            };
        }).ToList();

        return Ok(new
        {
            current,
            history,
            pending,
            inProgress,
            completed,
            cancelled,
            generatedAt = DateTime.Now.ToString("MMM d, yyyy, h:mm tt")
        });
    }
}