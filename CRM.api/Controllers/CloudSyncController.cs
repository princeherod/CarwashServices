using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CRM.Infrastructure.Data;
using CRM.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CRM.api.Controllers;

[ApiController]
[Route("api/cloud-sync")]
public class CloudSyncController : ControllerBase
{
    private readonly ICloudSyncService _cloudSyncService;
    private readonly MasterErpDbContext _db;

    public CloudSyncController(ICloudSyncService cloudSyncService, MasterErpDbContext db)
    {
        _cloudSyncService = cloudSyncService;
        _db = db;
    }

    private async Task<IActionResult?> ValidateSuperAdminAsync()
    {
        int callingUserId = 0;
        if (Request.Headers.TryGetValue("X-Current-User-Id", out var hVal) && int.TryParse(hVal.FirstOrDefault(), out var uid) && uid > 0)
            callingUserId = uid;
        else if (Request.Headers.TryGetValue("X-User-Id", out var hVal2) && int.TryParse(hVal2.FirstOrDefault(), out var uid2) && uid2 > 0)
            callingUserId = uid2;
        else if (Request.Query.TryGetValue("userId", out var qVal) && int.TryParse(qVal.FirstOrDefault(), out var qUid) && qUid > 0)
            callingUserId = qUid;

        if (callingUserId <= 0)
        {
            return StatusCode(401, new { message = "Unauthorized: Super Admin credentials required (X-Current-User-Id header missing)." });
        }

        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == callingUserId);
        if (user == null || user.RoleId != 1)
        {
            return StatusCode(403, new { message = "Forbidden: Database synchronization is strictly restricted to Super Admin (RoleId = 1)." });
        }

        return null;
    }

    [HttpGet("status")]
    public async Task<IActionResult> GetStatus(CancellationToken cancellationToken)
    {
        var status = await _cloudSyncService.GetSyncStatusAsync(cancellationToken);
        return Ok(status);
    }

    [HttpGet("connectivity")]
    public async Task<IActionResult> GetConnectivity(CancellationToken cancellationToken)
    {
        var status = await _cloudSyncService.CheckAllCloudDatabasesOnlineAsync(cancellationToken);
        return Ok(status);
    }

    [HttpGet("preview")]
    public async Task<IActionResult> GetPreview([FromQuery] string target = "All", CancellationToken cancellationToken = default)
    {
        var authErr = await ValidateSuperAdminAsync();
        if (authErr != null) return authErr;

        try
        {
            var previews = await _cloudSyncService.GenerateSyncPreviewAsync(target, cancellationToken);
            return Ok(previews);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = $"Failed to generate sync preview: {ex.Message}" });
        }
    }

    [HttpPost("sync")]
    public async Task<IActionResult> ExecuteSync([FromQuery] string target = "All", CancellationToken cancellationToken = default)
    {
        var authErr = await ValidateSuperAdminAsync();
        if (authErr != null) return authErr;

        try
        {
            var result = await _cloudSyncService.ExecuteSyncAsync(target, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = $"Failed to execute cloud synchronization: {ex.Message}" });
        }
    }

    [HttpPost("sync-now")]
    public async Task<IActionResult> SyncNow(CancellationToken cancellationToken)
    {
        var authErr = await ValidateSuperAdminAsync();
        if (authErr != null) return authErr;

        var result = await _cloudSyncService.ProcessPendingSyncQueueAsync(cancellationToken);
        return Ok(result);
    }

    [HttpPost("baseline-sync")]
    public async Task<IActionResult> BaselineSync(CancellationToken cancellationToken)
    {
        var authErr = await ValidateSuperAdminAsync();
        if (authErr != null) return authErr;

        var result = await _cloudSyncService.RunBaselineSyncAsync(cancellationToken);
        return Ok(result);
    }
}
