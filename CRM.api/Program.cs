using Microsoft.EntityFrameworkCore;
using CRM.Infrastructure.Data;
using CRM.Infrastructure.Services;
using CRM.domain.Entities;
using CRM.Domain.Entities;

var builder = WebApplication.CreateBuilder(args);

// -----------------------------------------------------------------
// DbContext registrations
// -----------------------------------------------------------------
builder.Services.AddDbContext<MasterErpDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("MasterErp")));

// NOTE: TenantErpDbContext is intentionally NOT registered here.
// It is created per-tenant by TenantDbContextFactory.

// -----------------------------------------------------------------
// Multi-tenant service registrations
// -----------------------------------------------------------------
builder.Services.AddScoped<ITenantDatabaseResolver, TenantDatabaseResolver>();
builder.Services.AddScoped<ITenantDbContextFactory, TenantDbContextFactory>();

// -----------------------------------------------------------------
// MVC / OpenAPI
// -----------------------------------------------------------------
builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

// -----------------------------------------------------------------
// Pipeline
// -----------------------------------------------------------------
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

// =================================================================
// Dev helper — seed a Company + CompanyDatabase (keep during dev only)
// =================================================================
app.MapPost("/seed/company-with-database", async (MasterErpDbContext db) =>
{
    var company = await db.Companies
        .FirstOrDefaultAsync(c => c.CompanyCode == "COMP001");

    if (company is null)
    {
        company = new Company
        {
            CompanyCode = "COMP001",
            CompanyName = "My First Company",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        db.Companies.Add(company);
        await db.SaveChangesAsync();
    }

    var companyDb = await db.CompanyDatabases
        .FirstOrDefaultAsync(d => d.CompanyId == company.CompanyId);

    if (companyDb is null)
    {
        companyDb = new CompanyDatabase
        {
            CompanyId = company.CompanyId,
            ServerName = "(localdb)\\MSSQLLocalDB",
            DatabaseName = "TenantErpDb",
            IsActive = true,
            CredentialKey = "TenantA"
        };
        db.CompanyDatabases.Add(companyDb);
        await db.SaveChangesAsync();
    }

    return Results.Ok(new
    {
        company.CompanyId,
        company.CompanyCode,
        companyDb.CompanyDatabaseId,
        companyDb.ServerName,
        companyDb.DatabaseName,
        companyDb.CredentialKey
    });
});

app.Run();