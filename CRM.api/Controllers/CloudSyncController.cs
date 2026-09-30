using System.Threading;
using System.Threading.Tasks;
using CRM.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;

namespace CRM.api.Controllers;

[ApiController]
[Route("api/cloud-sync")]
public class CloudSyncController : ControllerBase
{
    private readonly ICloudSyncService _cloudSyncService;

    public CloudSyncController(ICloudSyncService cloudSyncService)
    {
        _cloudSyncService = cloudSyncService;
    }

    [HttpGet("status")]
    public async Task<IActionResult> GetStatus(CancellationToken cancellationToken)
    {
        var status = await _cloudSyncService.GetSyncStatusAsync(cancellationToken);
        return Ok(status);
    }

    [HttpPost("sync-now")]
    public async Task<IActionResult> SyncNow(CancellationToken cancellationToken)
    {
        var result = await _cloudSyncService.ProcessPendingSyncQueueAsync(cancellationToken);
        return Ok(result);
    }

    [HttpPost("baseline-sync")]
    public async Task<IActionResult> BaselineSync(CancellationToken cancellationToken)
    {
        var result = await _cloudSyncService.RunBaselineSyncAsync(cancellationToken);
        return Ok(result);
    }
}
