using System;
using System.Linq;
using System.Threading.Tasks;
using CRM.domain.Entities;
using CRM.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace CRM.api.Services;

public class BranchAccessResult
{
    public bool Allowed { get; set; }
    public int? EffectiveBranchId { get; set; }
    public string? ErrorMessage { get; set; }
    public User? CallingUser { get; set; }
}

public static class BranchSecurityHelper
{
    public static async Task<BranchAccessResult> ResolveAndValidateAsync(
        MasterErpDbContext masterDb,
        HttpContext context,
        int targetCompanyId,
        int? requestedBranchId)
    {
        // 1. Resolve calling User ID from headers or query string
        int userId = 0;
        if (context.Request.Headers.TryGetValue("X-User-Id", out var hVal) && int.TryParse(hVal.FirstOrDefault(), out var uid) && uid > 0)
            userId = uid;
        else if (context.Request.Headers.TryGetValue("X-Current-User-Id", out var hVal2) && int.TryParse(hVal2.FirstOrDefault(), out var uid2) && uid2 > 0)
            userId = uid2;
        else if (context.Request.Query.TryGetValue("userId", out var qVal) && int.TryParse(qVal.FirstOrDefault(), out var qUid) && qUid > 0)
            userId = qUid;

        if (userId <= 0)
        {
            // Anonymous / legacy request: allow requested branch
            return new BranchAccessResult
            {
                Allowed = true,
                EffectiveBranchId = (requestedBranchId.HasValue && requestedBranchId.Value > 0) ? requestedBranchId.Value : null
            };
        }

        var user = await masterDb.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == userId);
        if (user == null)
        {
            return new BranchAccessResult
            {
                Allowed = true,
                EffectiveBranchId = (requestedBranchId.HasValue && requestedBranchId.Value > 0) ? requestedBranchId.Value : null
            };
        }

        // Super Admin (Role 4) has platform-wide access
        if (user.RoleId == 4)
        {
            return new BranchAccessResult
            {
                Allowed = true,
                EffectiveBranchId = (requestedBranchId.HasValue && requestedBranchId.Value > 0) ? requestedBranchId.Value : null,
                CallingUser = user
            };
        }

        // 2. Tenant Isolation Enforcement:
        // A user belonging to company A can NEVER access company B data
        if (user.CompanyId.HasValue && user.CompanyId.Value != targetCompanyId)
        {
            return new BranchAccessResult
            {
                Allowed = false,
                ErrorMessage = $"Access Denied: Cross-tenant access forbidden. User belongs to company {user.CompanyId}, target company is {targetCompanyId}.",
                CallingUser = user
            };
        }

        // 3. Branch Scoping Enforcement:
        if (user.BranchId.HasValue && user.BranchId.Value > 0)
        {
            // User is a Single Branch User locked to their assigned branch
            if (requestedBranchId.HasValue && requestedBranchId.Value > 0 && requestedBranchId.Value != user.BranchId.Value)
            {
                return new BranchAccessResult
                {
                    Allowed = false,
                    ErrorMessage = $"Access Denied: You are only authorized to access your assigned branch (Branch ID: {user.BranchId.Value}).",
                    CallingUser = user
                };
            }

            // Always enforce their assigned branch
            return new BranchAccessResult
            {
                Allowed = true,
                EffectiveBranchId = user.BranchId.Value,
                CallingUser = user
            };
        }

        // Multi-Branch Authorized User (BranchId is null):
        // Authorized to view all branches or filter by requested branch
        return new BranchAccessResult
        {
            Allowed = true,
            EffectiveBranchId = (requestedBranchId.HasValue && requestedBranchId.Value > 0) ? requestedBranchId.Value : null,
            CallingUser = user
        };
    }
}
