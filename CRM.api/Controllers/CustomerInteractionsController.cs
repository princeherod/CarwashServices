using CRM.Domain.Entities;
using CRM.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CRM.api.Controllers;

[ApiController]
[Route("api/tenant/{companyId:int}/customer-interactions")]
public class CustomerInteractionsController : ControllerBase
{
    private readonly ITenantDbContextFactory _tenantFactory;

    public CustomerInteractionsController(ITenantDbContextFactory tenantFactory)
    {
        _tenantFactory = tenantFactory;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(int companyId, [FromQuery] int? customerId = null)
    {
        await using var db = await _tenantFactory.CreateAsync(companyId);
        var q = db.CustomerInteractions.AsNoTracking().AsQueryable();
        if (customerId.HasValue) q = q.Where(x => x.CustomerId == customerId.Value);
        var rows = await q.OrderByDescending(x => x.CreatedAt).ToListAsync();
        return Ok(rows);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int companyId, int id)
    {
        await using var db = await _tenantFactory.CreateAsync(companyId);
        var row = await db.CustomerInteractions.AsNoTracking()
            .FirstOrDefaultAsync(x => x.InteractionId == id);
        if (row is null) return NotFound(new { message = $"Interaction {id} not found." });
        return Ok(row);
    }

    [HttpPost]
    public async Task<IActionResult> Create(int companyId, [FromBody] CustomerInteraction req)
    {
        await using var db = await _tenantFactory.CreateAsync(companyId);
        req.CreatedAt = DateTime.UtcNow;
        if (string.IsNullOrWhiteSpace(req.Status)) req.Status = "Open";
        db.CustomerInteractions.Add(req);
        await db.SaveChangesAsync();
        return Ok(req);
    }

    public class InteractionUpdateRequest
    {
        public string? Title { get; set; }
        public string? Details { get; set; }
        public string? Severity { get; set; }
        public string? Status { get; set; }
    }

    // PUT: api/tenant/1/customer-interactions/5
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int companyId, int id, [FromBody] InteractionUpdateRequest req)
    {
        await using var db = await _tenantFactory.CreateAsync(companyId);

        var existing = await db.CustomerInteractions
            .FirstOrDefaultAsync(x => x.InteractionId == id);

        if (existing is null)
            return NotFound(new { message = $"Interaction {id} not found." });

        if (!string.IsNullOrWhiteSpace(req.Title)) existing.Title = req.Title.Trim();
        if (req.Details != null) existing.Details = req.Details.Trim();
        if (!string.IsNullOrWhiteSpace(req.Severity)) existing.Severity = req.Severity;
        if (!string.IsNullOrWhiteSpace(req.Status)) existing.Status = req.Status;

        await db.SaveChangesAsync();
        return Ok(existing);
    }
}