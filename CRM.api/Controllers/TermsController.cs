using System;
using System.Linq;
using System.Threading.Tasks;
using CRM.domain.Entities;
using CRM.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CRM.api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TermsController : ControllerBase
    {
        private readonly MasterErpDbContext _db;

        public TermsController(MasterErpDbContext db)
        {
            _db = db;
        }

        // =====================================================================
        // GET /api/terms (newest first)
        // =====================================================================
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var list = await _db.TermsConditions
                .AsNoTracking()
                .Include(t => t.CreatedByUser)
                .OrderByDescending(t => t.TermsId)
                .Select(t => new
                {
                    termsId = t.TermsId,
                    version = t.Version,
                    effectiveDate = t.EffectiveDate,
                    effectiveDateFormatted = t.EffectiveDate.ToString("yyyy-MM-dd"),
                    createdBy = t.CreatedBy,
                    createdByName = t.CreatedByUser != null ? t.CreatedByUser.FullName : "User #" + t.CreatedBy,
                    content = t.Content
                })
                .ToListAsync();

            return Ok(list);
        }

        // =====================================================================
        // GET /api/terms/{id}
        // =====================================================================
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var item = await _db.TermsConditions
                .AsNoTracking()
                .Include(t => t.CreatedByUser)
                .FirstOrDefaultAsync(t => t.TermsId == id);

            if (item == null) return NotFound(new { message = $"Terms with ID {id} not found." });

            return Ok(new
            {
                termsId = item.TermsId,
                version = item.Version,
                effectiveDate = item.EffectiveDate,
                effectiveDateFormatted = item.EffectiveDate.ToString("yyyy-MM-dd"),
                createdBy = item.CreatedBy,
                createdByName = item.CreatedByUser != null ? item.CreatedByUser.FullName : "User #" + item.CreatedBy,
                content = item.Content
            });
        }

        // =====================================================================
        // GET /api/terms/latest
        // =====================================================================
        [HttpGet("latest")]
        public async Task<IActionResult> GetLatest()
        {
            var item = await _db.TermsConditions
                .AsNoTracking()
                .Include(t => t.CreatedByUser)
                .OrderByDescending(t => t.TermsId)
                .FirstOrDefaultAsync();

            if (item == null) return NotFound(new { message = "No terms found." });

            return Ok(new
            {
                termsId = item.TermsId,
                version = item.Version,
                effectiveDate = item.EffectiveDate,
                effectiveDateFormatted = item.EffectiveDate.ToString("yyyy-MM-dd"),
                createdBy = item.CreatedBy,
                createdByName = item.CreatedByUser != null ? item.CreatedByUser.FullName : "User #" + item.CreatedBy,
                content = item.Content
            });
        }

        // =====================================================================
        // POST /api/terms
        // =====================================================================
        [HttpPost]
        public async Task<IActionResult> CreateTerms([FromBody] CreateTermsRequest? request)
        {
            int userId = request?.CreatedBy ?? 0;

            // Resolve valid user ID
            if (userId <= 0 || !await _db.Users.AnyAsync(u => u.UserId == userId))
            {
                var fallbackUser = await _db.Users
                    .OrderBy(u => u.RoleId) // SuperAdmin role 1 preferred
                    .FirstOrDefaultAsync();

                if (fallbackUser != null)
                {
                    userId = fallbackUser.UserId;
                }
                else
                {
                    return BadRequest(new { message = "No valid user found to publish terms." });
                }
            }

            string version = request?.Version?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(version))
            {
                var latest = await _db.TermsConditions
                    .OrderByDescending(t => t.TermsId)
                    .FirstOrDefaultAsync();

                version = latest != null ? IncrementVersion(latest.Version) : "v1.0";
            }

            string content = request?.Content ?? "";
            if (string.IsNullOrWhiteSpace(content))
            {
                // Copy latest content if blank
                var latest = await _db.TermsConditions
                    .OrderByDescending(t => t.TermsId)
                    .FirstOrDefaultAsync();
                content = latest?.Content ?? "AQUASHINE CARWASH CRM - TERMS & CONDITIONS OF SERVICE\n\n1. ACCEPTANCE OF TERMS\n...";
            }

            var terms = new TermsCondition
            {
                Version = version,
                Content = content,
                EffectiveDate = DateTime.UtcNow.Date,
                CreatedBy = userId
            };

            _db.TermsConditions.Add(terms);
            await _db.SaveChangesAsync();

            var author = await _db.Users.FirstOrDefaultAsync(u => u.UserId == userId);

            return CreatedAtAction(nameof(GetById), new { id = terms.TermsId }, new
            {
                termsId = terms.TermsId,
                version = terms.Version,
                effectiveDate = terms.EffectiveDate,
                effectiveDateFormatted = terms.EffectiveDate.ToString("yyyy-MM-dd"),
                createdBy = terms.CreatedBy,
                createdByName = author?.FullName ?? "User #" + terms.CreatedBy,
                content = terms.Content,
                message = "New version of Terms & Conditions successfully published."
            });
        }

        private static string IncrementVersion(string current)
        {
            if (string.IsNullOrWhiteSpace(current)) return "v1.0";
            current = current.Trim();
            bool hasPrefix = current.StartsWith("v", StringComparison.OrdinalIgnoreCase);
            string numPart = hasPrefix ? current.Substring(1) : current;
            var parts = numPart.Split('.');
            if (parts.Length >= 2 && int.TryParse(parts[0], out int major) && int.TryParse(parts[1], out int minor))
            {
                return $"{(hasPrefix ? "v" : "")}{major}.{minor + 1}";
            }
            if (int.TryParse(numPart, out int single))
            {
                return $"{(hasPrefix ? "v" : "")}{single + 1}.0";
            }
            return $"v{DateTime.UtcNow:yyyy.MM}";
        }

        // =====================================================================
        // POST /api/terms/accept
        // =====================================================================
        [HttpPost("accept")]
        public async Task<IActionResult> AcceptTerms([FromBody] AcceptTermsRequest? req)
        {
            if (req == null || req.CompanyId <= 0)
                return BadRequest(new { message = "CompanyId is required." });

            var company = await _db.Companies.FirstOrDefaultAsync(c => c.CompanyId == req.CompanyId);
            if (company == null)
                return NotFound(new { message = $"Company with ID {req.CompanyId} not found." });

            var user = await _db.Users.FirstOrDefaultAsync(u => u.UserId == req.UserId);
            string acceptedBy = user != null ? $"{user.FullName} ({user.Email})" : $"User #{req.UserId}";

            string version = req.Version?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(version))
            {
                var latest = await _db.TermsConditions.OrderByDescending(t => t.TermsId).FirstOrDefaultAsync();
                version = latest?.Version ?? "v1.0";
            }

            company.TermsAccepted = true;
            company.TermsAcceptedAt = DateTime.UtcNow;
            company.TermsAcceptedBy = acceptedBy;
            company.TermsAcceptedVersion = version;

            await _db.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                message = "Terms & Conditions accepted successfully.",
                companyId = company.CompanyId,
                termsAccepted = true,
                termsAcceptedAt = company.TermsAcceptedAt,
                termsAcceptedBy = company.TermsAcceptedBy,
                termsAcceptedVersion = company.TermsAcceptedVersion
            });
        }

        // =====================================================================
        // POST /api/terms/decline
        // =====================================================================
        [HttpPost("decline")]
        public async Task<IActionResult> DeclineTerms([FromBody] DeclineTermsRequest? req)
        {
            if (req == null || req.CompanyId <= 0)
                return BadRequest(new { message = "CompanyId is required." });

            var company = await _db.Companies.FirstOrDefaultAsync(c => c.CompanyId == req.CompanyId);
            if (company == null)
                return NotFound(new { message = $"Company with ID {req.CompanyId} not found." });

            company.TermsAccepted = false;
            await _db.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                message = "Terms & Conditions declined. Access to CRM remains restricted until terms are accepted.",
                companyId = company.CompanyId,
                termsAccepted = false
            });
        }

        // =====================================================================
        // GET /api/terms/company/{companyId}
        // =====================================================================
        [HttpGet("company/{companyId:int}")]
        public async Task<IActionResult> GetCompanyTermsStatus(int companyId)
        {
            var company = await _db.Companies
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.CompanyId == companyId);

            if (company == null)
                return NotFound(new { message = $"Company with ID {companyId} not found." });

            var latestTerms = await _db.TermsConditions
                .AsNoTracking()
                .OrderByDescending(t => t.TermsId)
                .FirstOrDefaultAsync();

            return Ok(new
            {
                companyId = company.CompanyId,
                companyCode = company.CompanyCode,
                companyName = company.CompanyName,
                termsAccepted = company.TermsAccepted,
                termsAcceptedAt = company.TermsAcceptedAt,
                termsAcceptedBy = company.TermsAcceptedBy,
                termsAcceptedVersion = company.TermsAcceptedVersion,
                latestVersion = latestTerms?.Version ?? "v1.0",
                latestTermsId = latestTerms?.TermsId,
                needsAcceptance = !company.TermsAccepted
            });
        }
    }

    public class CreateTermsRequest
    {
        public string? Content { get; set; }
        public int? CreatedBy { get; set; }
        public string? Version { get; set; }
    }

    public class AcceptTermsRequest
    {
        public int CompanyId { get; set; }
        public int UserId { get; set; }
        public int? TermsId { get; set; }
        public string? Version { get; set; }
    }

    public class DeclineTermsRequest
    {
        public int CompanyId { get; set; }
        public int UserId { get; set; }
        public string? Reason { get; set; }
    }
}
