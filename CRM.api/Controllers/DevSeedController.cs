using CRM.domain.Entities;
using CRM.Domain.Entities;
using CRM.Infrastructure.Data;
using CRM.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CRM.api.Controllers;

/// <summary>
/// Dev-only helper that generates fake service requests (transactions) and
/// matching billing rows so Reports / Analytics have something to show.
/// Guarded by ?seed= to be reproducible. Do NOT expose in production.
/// </summary>
[ApiController]
[Route("api/dev/seed")]
public class DevSeedController : ControllerBase
{
    private readonly MasterErpDbContext _master;
    private readonly ITenantDbContextFactory _tenantFactory;

    public DevSeedController(MasterErpDbContext master, ITenantDbContextFactory tenantFactory)
    {
        _master = master;
        _tenantFactory = tenantFactory;
    }

    // ================================================================
    //  POST  api/dev/seed/transactions?companyId=1&count=200&seed=42
    // ================================================================
    [HttpPost("transactions")]
    public async Task<IActionResult> SeedTransactions(
        [FromQuery] int companyId = 1,
        [FromQuery] int count = 200,
        [FromQuery] int? seed = null)
    {
        if (count <= 0 || count > 2000)
            return BadRequest(new { message = "count must be between 1 and 2000." });

        var rnd = seed.HasValue ? new Random(seed.Value) : new Random();

        // ---- Load tenant data (customers + services) ----
        List<TenantCustomer> customers;
        List<Product> services;
        await using (var tenant = await _tenantFactory.CreateAsync(companyId))
        {
            customers = await tenant.TenantCustomers.AsNoTracking().ToListAsync();
            services = await tenant.Products.AsNoTracking()
                                              .Where(p => p.IsActive)
                                              .ToListAsync();
        }

        if (customers.Count == 0)
            return BadRequest(new { message = "No customers found in tenant. Create customers first." });
        if (services.Count == 0)
            return BadRequest(new { message = "No active services found in tenant. Create services first." });

        // ---- Load master users (creator + staff pool) ----
        var allUsers = await _master.Users.AsNoTracking().ToListAsync();
        if (allUsers.Count == 0)
            return BadRequest(new { message = "No users found in master DB. Seed a user first." });

        int creatorId = allUsers.OrderBy(u => u.UserId).First().UserId;
        var staffIds = allUsers.Where(u => u.RoleId == 3).Select(u => u.UserId).ToList();
        if (staffIds.Count == 0)
            staffIds.Add(creatorId);   // fall back so AssignedStaffId is never null when we want it set

        // ---- Status distribution ----
        // 75% Completed | 10% Pending | 8% InProgress | 5% Cancelled | 2% Assigned
        string[] statusPool =
        {
            "Completed","Completed","Completed","Completed","Completed","Completed","Completed","Completed",
            "Completed","Completed","Completed","Completed","Completed","Completed","Completed","Completed",
            "Completed","Completed","Completed","Completed","Completed","Completed","Completed","Completed",
            "Completed","Completed","Completed","Completed","Completed","Completed","Completed","Completed",
            "Completed","Completed","Completed","Completed","Completed","Completed","Completed","Completed",
            "Completed","Completed","Completed","Completed","Completed","Completed","Completed","Completed",
            "Completed","Completed","Completed","Completed","Completed","Completed","Completed","Completed",
            "Completed","Completed","Completed","Completed","Completed","Completed","Completed","Completed",
            "Pending","Pending","Pending","Pending","Pending","Pending","Pending","Pending",
            "InProgress","InProgress","InProgress","InProgress","InProgress","InProgress",
            "Cancelled","Cancelled","Cancelled","Cancelled",
            "Assigned","Assigned"
        };

        string[] priorities = { "Normal", "Normal", "Normal", "High", "High", "VIP" };
        string[] notePool =
        {
            "Customer requested extra interior vacuum.",
            "Add tire shine.",
            "Please use the customer's own wax.",
            "Paid in cash — no receipt needed.",
            "Customer will wait in the lounge.",
            "Driver dropped off — call when done.",
            "Wash only, no wax this time.",
            "Include engine bay cleaning.",
            "Rush job — customer needs car by 5pm.",
            "Requested the same crew as last time."
        };

        DateTime today = DateTime.Today;

        var requests = new List<ServiceRequest>(count);
        var billings = new List<BillingTransaction>(count);
        var logs = new List<ServiceStatusLog>(count * 2);

        for (int i = 0; i < count; i++)
        {
            var cust = customers[rnd.Next(customers.Count)];
            var svc = services[rnd.Next(services.Count)];

            int daysAgo = rnd.Next(0, 180);
            var requested = today.AddDays(-daysAgo)
                                 .AddHours(rnd.Next(8, 18))
                                 .AddMinutes(rnd.Next(0, 60));

            var scheduled = requested.AddDays(rnd.Next(0, 3)).AddHours(rnd.Next(0, 5));
            var status = statusPool[rnd.Next(statusPool.Length)];
            var priority = priorities[rnd.Next(priorities.Length)];

            DateTime? completed = null;
            if (status == "Completed")
                completed = scheduled.AddHours(rnd.Next(1, 7)).AddMinutes(rnd.Next(0, 60));

            int? staffId = status == "Pending" || status == "Cancelled"
                ? null
                : staffIds[rnd.Next(staffIds.Count)];

            string? notes = rnd.NextDouble() < 0.4
                ? notePool[rnd.Next(notePool.Length)]
                : null;

            var req = new ServiceRequest
            {
                CustomerId = cust.TenantCustomerId,
                ServiceId = svc.ProductId,
                AssignedStaffId = staffId,
                CreatedBy = creatorId,
                Status = status,
                Priority = priority,
                RequestedDate = requested,
                ScheduledDate = scheduled,
                CompletedDate = completed,
                Notes = notes
            };
            requests.Add(req);

            // billing for completed rows
            if (status == "Completed")
            {
                billings.Add(new BillingTransaction
                {
                    // RequestId is wired below after SaveChanges assigns IDs
                    RequestId = 0,
                    Amount = svc.UnitPrice,
                    PaymentStatus = "Paid",
                    TransactionDate = completed ?? scheduled
                });
            }

            // status log(s)
            logs.Add(new ServiceStatusLog
            {
                RequestId = 0,           // wired below
                Status = "Pending",
                UpdatedBy = creatorId,
                UpdatedAt = requested,
                Notes = "Request created"
            });

            if (status != "Pending")
            {
                logs.Add(new ServiceStatusLog
                {
                    RequestId = 0,
                    Status = status,
                    UpdatedBy = staffId ?? creatorId,
                    UpdatedAt = completed ?? scheduled,
                    Notes = status == "Completed"
                        ? "Service completed"
                        : "Status updated"
                });
            }
        }

        // ---- Save requests first so we get their IDs ----
        _master.ServiceRequests.AddRange(requests);
        await _master.SaveChangesAsync();

        // ---- Wire up billing + logs to the new request IDs ----
        for (int i = 0; i < requests.Count; i++)
        {
            var req = requests[i];

            // Billing: we added them in order but only for Completed rows.
            // Just find the matching billing entry by index logic:
            // simpler: add billing rows directly now that we have IDs.
            if (req.Status == "Completed")
            {
                var svc = services.First(s => s.ProductId == req.ServiceId);
                _master.BillingTransactions.Add(new BillingTransaction
                {
                    RequestId = req.RequestId,
                    Amount = svc.UnitPrice,
                    PaymentStatus = "Paid",
                    TransactionDate = req.CompletedDate ?? req.ScheduledDate ?? req.RequestedDate
                });
            }
        }

        // ---- Wire status logs by matching the request order ----
        // requests list is in insertion order; logs were added in pairs
        // We'll just create fresh logs now to avoid index bookkeeping.
        foreach (var req in requests)
        {
            _master.ServiceStatusLogs.Add(new ServiceStatusLog
            {
                RequestId = req.RequestId,
                Status = "Pending",
                UpdatedBy = req.CreatedBy,
                UpdatedAt = req.RequestedDate,
                Notes = "Request created"
            });

            if (req.Status != "Pending")
            {
                _master.ServiceStatusLogs.Add(new ServiceStatusLog
                {
                    RequestId = req.RequestId,
                    Status = req.Status,
                    UpdatedBy = req.AssignedStaffId ?? req.CreatedBy,
                    UpdatedAt = req.CompletedDate ?? req.ScheduledDate ?? req.RequestedDate,
                    Notes = req.Status == "Completed" ? "Service completed" : "Status updated"
                });
            }
        }

        await _master.SaveChangesAsync();

        return Ok(new
        {
            message = "Seed complete.",
            requestsCreated = requests.Count,
            billingCreated = requests.Count(r => r.Status == "Completed"),
            logsCreated = requests.Sum(r => r.Status == "Pending" ? 1 : 2),
            customersUsed = customers.Count,
            servicesUsed = services.Count
        });
    }

    // ================================================================
    //  DELETE  api/dev/seed/transactions
    //  Wipes ServiceRequests + their BillingTransactions + StatusLogs.
    //  Handy for re-seeding without piling up rows.
    // ===========================================================  =====
    [HttpDelete("transactions")]
    public async Task<IActionResult> WipeTransactions()
    {
        var reqIds = await _master.ServiceRequests.Select(r => r.RequestId).ToListAsync();

        var billings = await _master.BillingTransactions
            .Where(b => b.RequestId != null && reqIds.Contains(b.RequestId.Value))
            .ToListAsync();

        var logs = await _master.ServiceStatusLogs
            .Where(l => reqIds.Contains(l.RequestId))
            .ToListAsync();

        var requests = await _master.ServiceRequests.ToListAsync();

        _master.BillingTransactions.RemoveRange(billings);
        _master.ServiceStatusLogs.RemoveRange(logs);
        _master.ServiceRequests.RemoveRange(requests);

        await _master.SaveChangesAsync();

        return Ok(new
        {
            message = "All seed transactions removed.",
            requestsRemoved = requests.Count,
            billingsRemoved = billings.Count,
            logsRemoved = logs.Count
        });
    }
}