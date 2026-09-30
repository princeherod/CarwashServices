using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CRM.Domain.Entities;
using CRM.domain.Entities;
using CRM.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CRM.api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CompaniesController : ControllerBase
{
    private readonly MasterErpDbContext _masterDb;
    private readonly CRM.Infrastructure.Services.ITenantDatabaseProvisioner _provisioner;
    private readonly Microsoft.Extensions.Logging.ILogger<CompaniesController> _logger;

    public CompaniesController(
        MasterErpDbContext masterDb,
        CRM.Infrastructure.Services.ITenantDatabaseProvisioner provisioner,
        Microsoft.Extensions.Logging.ILogger<CompaniesController> logger)
    {
        _masterDb = masterDb;
        _provisioner = provisioner;
        _logger = logger;
    }

    // DTO for creating companies with admin & DB setup
    public class InitialAdminRequest
    {
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? FullName { get; set; }
        public string Email { get; set; } = "";
        public string Password { get; set; } = "";
    }

    public class CreateCompanyRequest
    {
        public string CompanyCode { get; set; } = "";
        public string CompanyName { get; set; } = "";
        public string? ContactPhone { get; set; }
        public string? ContactEmail { get; set; }
        public string? AddressLine { get; set; }
        public string? City { get; set; }
        public string? Province { get; set; }
        public string? State { get; set; }
        public string? PostalCode { get; set; }
        public string? Country { get; set; }
        public string? DatabaseServer { get; set; }
        public string? DatabaseName { get; set; }
        public InitialAdminRequest? InitialAdmin { get; set; }
    }

    public class UpdateCompanyRequest
    {
        public string CompanyName { get; set; } = "";
        public string? ContactPhone { get; set; }
        public string? ContactEmail { get; set; }
        public string? AddressLine { get; set; }
        public string? City { get; set; }
        public string? Province { get; set; }
        public string? State { get; set; }
        public string? PostalCode { get; set; }
        public string? Country { get; set; }
        public bool? IsActive { get; set; }
        public int? AdminUserId { get; set; }
        public bool? UnassignAdmin { get; set; }
    }

    public class StatusUpdateRequest
    {
        public bool IsActive { get; set; }
    }

    // GET: api/companies
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        try
        {
            var companies = await _masterDb.Companies
                .AsNoTracking()
                .OrderBy(c => c.CompanyId)
                .ToListAsync();

            var companyIds = companies.Select(c => c.CompanyId).ToList();

            var users = await _masterDb.Users
                .AsNoTracking()
                .Where(u => u.CompanyId != null && companyIds.Contains(u.CompanyId.Value))
                .ToListAsync();

            var databases = await _masterDb.CompanyDatabases
                .AsNoTracking()
                .Where(d => companyIds.Contains(d.CompanyId))
                .ToListAsync();

            var result = companies.Select(c =>
            {
                var companyUsers = users.Where(u => u.CompanyId == c.CompanyId).ToList();
                var adminUser = companyUsers.FirstOrDefault(u => u.RoleId == 1) ?? companyUsers.FirstOrDefault();
                var dbConfig = databases.FirstOrDefault(d => d.CompanyId == c.CompanyId);

                return new
                {
                    companyId = c.CompanyId,
                    companyCode = c.CompanyCode,
                    companyName = c.CompanyName,
                    isActive = c.IsActive,
                    createdAt = c.CreatedAt,
                    contactPhone = c.ContactPhone,
                    contactEmail = c.ContactEmail,
                    addressLine = c.AddressLine,
                    city = c.City,
                    province = c.Province,
                    state = c.State,
                    postalCode = c.PostalCode,
                    country = c.Country,
                    adminCount = companyUsers.Count,
                    adminUserId = adminUser?.UserId,
                    adminUser = adminUser?.FullName ?? "Unassigned",
                    adminEmail = adminUser?.Email ?? "",
                    databaseServer = dbConfig?.ServerName,
                    databaseName = dbConfig?.DatabaseName,
                    termsAccepted = c.TermsAccepted,
                    termsAcceptedAt = c.TermsAcceptedAt,
                    termsAcceptedBy = c.TermsAcceptedBy,
                    termsAcceptedVersion = c.TermsAcceptedVersion
                };
            }).ToList();

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load company records in Super Admin.");
            return StatusCode(500, new { message = "Failed to load company records.", error = ex.Message });
        }
    }

    // GET: api/companies/next-code
    [HttpGet("next-code")]
    public async Task<IActionResult> GetNextCode()
    {
        try
        {
            var codes = await _masterDb.Companies
                .AsNoTracking()
                .Select(c => c.CompanyCode)
                .ToListAsync();

            int maxNum = 0;
            foreach (var code in codes)
            {
                if (string.IsNullOrWhiteSpace(code)) continue;
                var match = Regex.Match(code.Trim(), @"^COMP(\d+)$", RegexOptions.IgnoreCase);
                if (match.Success && int.TryParse(match.Groups[1].Value, out var num))
                {
                    if (num > maxNum) maxNum = num;
                }
            }

            string next = $"COMP{(maxNum + 1):D3}";
            return Ok(new { nextCode = next });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate next company code.");
            return StatusCode(500, new { message = "Failed to generate next code.", error = ex.Message });
        }
    }

    // GET: api/companies/1
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        try
        {
            var company = await _masterDb.Companies
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.CompanyId == id);

            if (company is null)
            {
                return NotFound(new { message = $"Company {id} not found." });
            }

            var companyUsers = await _masterDb.Users
                .AsNoTracking()
                .Where(u => u.CompanyId == id)
                .ToListAsync();

            var adminUser = companyUsers.FirstOrDefault(u => u.RoleId == 1) ?? companyUsers.FirstOrDefault();
            var dbConfig = await _masterDb.CompanyDatabases
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.CompanyId == id);

            return Ok(new
            {
                companyId = company.CompanyId,
                companyCode = company.CompanyCode,
                companyName = company.CompanyName,
                isActive = company.IsActive,
                createdAt = company.CreatedAt,
                contactPhone = company.ContactPhone,
                contactEmail = company.ContactEmail,
                addressLine = company.AddressLine,
                city = company.City,
                province = company.Province,
                state = company.State,
                postalCode = company.PostalCode,
                country = company.Country,
                adminCount = companyUsers.Count,
                adminUser = adminUser?.FullName ?? "Unassigned",
                adminEmail = adminUser?.Email ?? "",
                databaseServer = dbConfig?.ServerName,
                databaseName = dbConfig?.DatabaseName,
                termsAccepted = company.TermsAccepted,
                termsAcceptedAt = company.TermsAcceptedAt,
                termsAcceptedBy = company.TermsAcceptedBy,
                termsAcceptedVersion = company.TermsAcceptedVersion
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve company {CompanyId}.", id);
            return StatusCode(500, new { message = $"Failed to retrieve company {id}.", error = ex.Message });
        }
    }

    // POST: api/companies
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCompanyRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.CompanyName))
        {
            return BadRequest(new { message = "Company name is required." });
        }

        string code;
        if (!string.IsNullOrWhiteSpace(req.CompanyCode))
        {
            code = req.CompanyCode.Trim().ToUpperInvariant();
        }
        else
        {
            var codes = await _masterDb.Companies
                .Select(c => c.CompanyCode)
                .ToListAsync();

            int maxNum = 0;
            foreach (var c in codes)
            {
                if (string.IsNullOrWhiteSpace(c)) continue;
                var match = Regex.Match(c, @"COMP(\d+)", RegexOptions.IgnoreCase);
                if (match.Success && int.TryParse(match.Groups[1].Value, out var n))
                {
                    if (n > maxNum) maxNum = n;
                }
            }
            code = $"COMP{(maxNum + 1):D3}";
        }

        var exists = await _masterDb.Companies
            .AnyAsync(c => c.CompanyCode == code);

        if (exists)
        {
            return Conflict(new { message = $"A company with code '{code}' already exists." });
        }

        // Validate initial admin if provided
        if (req.InitialAdmin != null && !string.IsNullOrWhiteSpace(req.InitialAdmin.Email))
        {
            var adminEmail = req.InitialAdmin.Email.Trim().ToLowerInvariant();
            var emailExists = await _masterDb.Users.AnyAsync(u => u.Email.ToLower() == adminEmail);
            if (emailExists)
            {
                return Conflict(new { message = $"A user with email '{adminEmail}' already exists." });
            }
        }

        using var transaction = await _masterDb.Database.BeginTransactionAsync();

        var company = new Company
        {
            CompanyCode = code,
            CompanyName = req.CompanyName.Trim(),
            ContactPhone = req.ContactPhone?.Trim(),
            ContactEmail = req.ContactEmail?.Trim(),
            AddressLine = req.AddressLine?.Trim(),
            City = req.City?.Trim(),
            Province = req.Province?.Trim(),
            State = (req.State ?? req.Province)?.Trim(),
            PostalCode = req.PostalCode?.Trim(),
            Country = req.Country?.Trim(),
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _masterDb.Companies.Add(company);
        await _masterDb.SaveChangesAsync();

        // 1. Setup CompanyDatabase record
        var serverName = !string.IsNullOrWhiteSpace(req.DatabaseServer)
            ? req.DatabaseServer.Trim()
            : "(localdb)\\MSSQLLocalDB";

        var dbName = !string.IsNullOrWhiteSpace(req.DatabaseName)
            ? req.DatabaseName.Trim()
            : $"Tenant_{company.CompanyCode}";

        var compDb = new CompanyDatabase
        {
            CompanyId = company.CompanyId,
            ServerName = serverName,
            DatabaseName = dbName,
            CredentialKey = "TenantA",
            IsActive = true
        };
        _masterDb.CompanyDatabases.Add(compDb);

        // 2. Setup initial admin user if provided
        CRM.domain.Entities.User? createdAdmin = null;
        if (req.InitialAdmin != null && !string.IsNullOrWhiteSpace(req.InitialAdmin.Email))
        {
            var adminFirst = req.InitialAdmin.FirstName?.Trim();
            var adminLast = req.InitialAdmin.LastName?.Trim();

            if (string.IsNullOrWhiteSpace(adminFirst) || string.IsNullOrWhiteSpace(adminLast))
            {
                if (!string.IsNullOrWhiteSpace(req.InitialAdmin.FullName))
                {
                    var full = req.InitialAdmin.FullName.Trim();
                    var idx = full.IndexOf(' ');
                    if (idx > 0)
                    {
                        if (string.IsNullOrWhiteSpace(adminFirst)) adminFirst = full.Substring(0, idx).Trim();
                        if (string.IsNullOrWhiteSpace(adminLast)) adminLast = full.Substring(idx + 1).Trim();
                    }
                    else
                    {
                        if (string.IsNullOrWhiteSpace(adminFirst)) adminFirst = full;
                        if (string.IsNullOrWhiteSpace(adminLast)) adminLast = "-";
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(adminFirst)) adminFirst = "Admin";
            if (string.IsNullOrWhiteSpace(adminLast)) adminLast = "User";

            createdAdmin = new CRM.domain.Entities.User
            {
                IdentityUserId = string.Empty,
                FirstName = adminFirst,
                LastName = adminLast,
                Email = req.InitialAdmin.Email.Trim().ToLowerInvariant(),
                PasswordHash = req.InitialAdmin.Password,
                RoleId = 1, // Admin (Tenant Admin)
                Status = "Active",
                CreatedAt = DateTime.UtcNow,
                CompanyId = company.CompanyId
            };
            _masterDb.Users.Add(createdAdmin);
        }

        // 3. Setup default subscription if plans exist
        var defaultPlan = await _masterDb.TenantSubscriptionPlans
            .Where(p => p.IsActive && !p.IsArchived)
            .OrderBy(p => p.PlanId)
            .FirstOrDefaultAsync();

        if (defaultPlan != null)
        {
            var sub = new TenantSubscription
            {
                CompanyId = company.CompanyId,
                PlanId = defaultPlan.PlanId,
                Status = "Active",
                StartDate = DateTime.UtcNow,
                RenewalDate = DateTime.UtcNow.AddMonths(1),
                AutoRenew = true,
                CreatedAt = DateTime.UtcNow
            };
            _masterDb.TenantSubscriptions.Add(sub);
        }

        await _masterDb.SaveChangesAsync();
        await transaction.CommitAsync();

        // 4. Physically provision the tenant database on SQL Server
        try
        {
            await _provisioner.ProvisionDatabaseAsync(
                compDb.ServerName,
                compDb.DatabaseName,
                compDb.CredentialKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to physically provision tenant database '{DatabaseName}' on '{ServerName}'.", compDb.DatabaseName, compDb.ServerName);
        }

        var dto = new
        {
            companyId = company.CompanyId,
            companyCode = company.CompanyCode,
            companyName = company.CompanyName,
            isActive = company.IsActive,
            createdAt = company.CreatedAt,
            contactPhone = company.ContactPhone,
            contactEmail = company.ContactEmail,
            addressLine = company.AddressLine,
            city = company.City,
            province = company.Province,
            state = company.State,
            postalCode = company.PostalCode,
            country = company.Country,
            adminCount = createdAdmin != null ? 1 : 0,
            adminUser = createdAdmin?.FullName ?? "Unassigned",
            adminEmail = createdAdmin?.Email ?? "",
            databaseServer = compDb.ServerName,
            databaseName = compDb.DatabaseName
        };

        return CreatedAtAction(nameof(GetById), new { id = company.CompanyId }, dto);
    }

    // PUT: api/companies/5
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateCompanyRequest req)
    {
        var company = await _masterDb.Companies.FirstOrDefaultAsync(c => c.CompanyId == id);
        if (company is null)
        {
            return NotFound(new { message = $"Company {id} not found." });
        }

        if (string.IsNullOrWhiteSpace(req.CompanyName))
        {
            return BadRequest(new { message = "Company name is required." });
        }

        company.CompanyName = req.CompanyName.Trim();
        company.ContactPhone = req.ContactPhone?.Trim();
        company.ContactEmail = req.ContactEmail?.Trim();
        company.AddressLine = req.AddressLine?.Trim();
        company.City = req.City?.Trim();
        company.Province = req.Province?.Trim();
        company.State = (req.State ?? req.Province)?.Trim();
        company.PostalCode = req.PostalCode?.Trim();
        company.Country = req.Country?.Trim();

        if (req.IsActive.HasValue)
        {
            company.IsActive = req.IsActive.Value;
        }

        // Handle Admin assignment / unassignment
        if (req.UnassignAdmin == true || (req.AdminUserId.HasValue && req.AdminUserId.Value == 0))
        {
            var currentAdmins = await _masterDb.Users.Where(u => u.CompanyId == id).ToListAsync();
            foreach (var adm in currentAdmins)
            {
                adm.CompanyId = null;
            }
        }
        else if (req.AdminUserId.HasValue && req.AdminUserId.Value > 0)
        {
            // Unassign other users for this company
            var currentAdmins = await _masterDb.Users.Where(u => u.CompanyId == id && u.UserId != req.AdminUserId.Value).ToListAsync();
            foreach (var adm in currentAdmins)
            {
                adm.CompanyId = null;
            }

            var targetUser = await _masterDb.Users.FirstOrDefaultAsync(u => u.UserId == req.AdminUserId.Value);
            if (targetUser != null)
            {
                targetUser.CompanyId = company.CompanyId;
                if (targetUser.RoleId != 4)
                {
                    targetUser.RoleId = 1; // Admin
                }
            }
        }

        await _masterDb.SaveChangesAsync();
        return Ok(company);
    }

    // GET: api/companies/available-admins
    [HttpGet("available-admins")]
    public async Task<IActionResult> GetAvailableAdmins([FromQuery] int? companyId = null)
    {
        var admins = await _masterDb.Users
            .AsNoTracking()
            .Where(u => u.RoleId == 1 || u.RoleId == 2 || u.RoleId == 4)
            .Select(u => new
            {
                userId = u.UserId,
                fullName = u.FullName,
                email = u.Email,
                roleId = u.RoleId,
                companyId = u.CompanyId,
                isCurrentCompany = companyId.HasValue && u.CompanyId == companyId.Value
            })
            .OrderBy(u => u.fullName)
            .ToListAsync();

        return Ok(admins);
    }

    // PUT: api/companies/5/status
    [HttpPut("{id:int}/status")]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] StatusUpdateRequest req)
    {
        var company = await _masterDb.Companies.FirstOrDefaultAsync(c => c.CompanyId == id);
        if (company is null)
        {
            return NotFound(new { message = $"Company {id} not found." });
        }

        company.IsActive = req.IsActive;

        // If deactivated, we can also update user statuses or let them be blocked on login
        await _masterDb.SaveChangesAsync();

        return Ok(new
        {
            companyId = company.CompanyId,
            isActive = company.IsActive,
            message = company.IsActive ? "Company activated successfully." : "Company deactivated successfully."
        });
    }

    // DELETE: api/companies/5
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var company = await _masterDb.Companies.FirstOrDefaultAsync(c => c.CompanyId == id);
        if (company is null)
        {
            return NotFound(new { message = $"Company {id} not found." });
        }

        // Soft delete: deactivate
        company.IsActive = false;
        await _masterDb.SaveChangesAsync();

        return NoContent();
    }

    // POST: api/companies/{id}/provision-database
    [HttpPost("{id:int}/provision-database")]
    public async Task<IActionResult> ProvisionDatabaseAsync(int id)
    {
        var compDb = await _masterDb.CompanyDatabases
            .FirstOrDefaultAsync(d => d.CompanyId == id && d.IsActive);

        if (compDb == null)
        {
            return NotFound(new { message = $"No active database configuration found for Company ID {id}." });
        }

        try
        {
            await _provisioner.ProvisionDatabaseAsync(
                compDb.ServerName,
                compDb.DatabaseName,
                compDb.CredentialKey);

            return Ok(new
            {
                message = $"Database '{compDb.DatabaseName}' provisioned successfully on '{compDb.ServerName}'.",
                databaseName = compDb.DatabaseName,
                serverName = compDb.ServerName
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Provisioning failed for company {CompanyId}", id);
            return StatusCode(500, new { message = $"Failed to provision database: {ex.Message}" });
        }
    }

    // POST: api/companies/provision-all
    [HttpPost("provision-all")]
    public async Task<IActionResult> ProvisionAllDatabasesAsync()
    {
        try
        {
            await _provisioner.EnsureAllDatabasesProvisionedAsync();
            return Ok(new { message = "All tenant databases have been checked and provisioned on SQL Server." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error provisioning all databases");
            return StatusCode(500, new { message = $"Failed to provision all databases: {ex.Message}" });
        }
    }
}