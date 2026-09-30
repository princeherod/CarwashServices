using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CRM.Infrastructure.Services;

public class CloudSyncStatusDto
{
    public bool IsCloudOnline { get; set; }
    public int PendingCount { get; set; }
    public int SyncedCount { get; set; }
    public int FailedCount { get; set; }
    public DateTime? LastSyncTime { get; set; }
    public string CloudServer { get; set; } = string.Empty;
    public string CloudDatabase { get; set; } = string.Empty;
    public string StatusMessage { get; set; } = string.Empty;
}

public class SyncResult
{
    public bool Success { get; set; }
    public bool IsCloudOnline { get; set; }
    public int ProcessedCount { get; set; }
    public int SuccessCount { get; set; }
    public int FailureCount { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<string> Errors { get; set; } = new();
}

public interface ICloudSyncService
{
    Task<bool> IsCloudAvailableAsync(CancellationToken cancellationToken = default);
    Task QueueAndSyncRecordAsync(string tableName, int? companyId, string recordKey, string operation, object payload, CancellationToken cancellationToken = default);
    Task<SyncResult> ProcessPendingSyncQueueAsync(CancellationToken cancellationToken = default);
    Task<SyncResult> RunBaselineSyncAsync(CancellationToken cancellationToken = default);
    Task<CloudSyncStatusDto> GetSyncStatusAsync(CancellationToken cancellationToken = default);
}
