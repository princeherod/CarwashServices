using CRM.api.Services;
using CRM.domain.Entities;
using CRM.Domain.Entities;
using CRM.Infrastructure.Data;
using CRM.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// -----------------------------------------------------------------
// DbContext registrations
// -----------------------------------------------------------------
builder.Services.AddDbContext<MasterErpDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("MasterErp"),
        sql => sql.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(30),
            errorNumbersToAdd: null)));

// NOTE: TenantErpDbContext is intentionally NOT registered here.
// It is created per-tenant by TenantDbContextFactory.

// -----------------------------------------------------------------
// Multi-tenant service registrations
// -----------------------------------------------------------------
builder.Services.AddScoped<ITenantDatabaseResolver, TenantDatabaseResolver>();
builder.Services.AddScoped<ITenantDbContextFactory, TenantDbContextFactory>();
builder.Services.AddScoped<ITenantDatabaseProvisioner, TenantDatabaseProvisioner>();
builder.Services.Configure<SmtpOptions>(builder.Configuration.GetSection("Smtp"));
builder.Services.AddSingleton<IEmailSender, SmtpEmailSender>();
builder.Services.AddSingleton<ICloudSyncService, CloudSyncService>();
builder.Services.AddHostedService<CloudSyncBackgroundService>();

// -----------------------------------------------------------------
// MVC / OpenAPI
// -----------------------------------------------------------------
builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
});
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

// Ensure all tenant databases are physically provisioned on SQL Server
using (var scope = app.Services.CreateScope())
{
    var provisioner = scope.ServiceProvider.GetRequiredService<ITenantDatabaseProvisioner>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    try
    {
        await provisioner.EnsureAllDatabasesProvisionedAsync();
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to provision tenant databases during startup.");
    }
}

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
            DatabaseName = "TenantCrmDb",
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