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

public class SyncTablePreviewDto
{
    public string TableName { get; set; } = string.Empty;
    public int NewCount { get; set; }
    public int UpdatedCount { get; set; }
    public int UnchangedCount { get; set; }
    public int CloudOnlyCount { get; set; }
}

public class SyncDatabasePreviewDto
{
    public string Target { get; set; } = string.Empty;
    public string SourceDatabase { get; set; } = string.Empty;
    public string DestinationDatabase { get; set; } = string.Empty;
    public string DestinationServer { get; set; } = string.Empty;
    public bool IsCloudOnline { get; set; }
    public string? ConnectionError { get; set; }
    public List<SyncTablePreviewDto> Tables { get; set; } = new();
    public int TotalNew { get; set; }
    public int TotalUpdated { get; set; }
    public int TotalUnchanged { get; set; }
    public int TotalCloudOnly { get; set; }
}

public class SyncTableResultDto
{
    public string TableName { get; set; } = string.Empty;
    public int Inserted { get; set; }
    public int Updated { get; set; }
    public int Skipped { get; set; }
    public int Failed { get; set; }
    public string? ErrorMessage { get; set; }
}

public class SyncDatabaseResultDto
{
    public string Target { get; set; } = string.Empty;
    public string SourceDatabase { get; set; } = string.Empty;
    public string DestinationDatabase { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string Duration { get; set; } = string.Empty;
    public List<SyncTableResultDto> Tables { get; set; } = new();
    public int TotalInserted { get; set; }
    public int TotalUpdated { get; set; }
    public int TotalSkipped { get; set; }
    public int TotalFailed { get; set; }
    public string? ErrorMessage { get; set; }
}

public class MultiSyncResultDto
{
    public bool Success { get; set; }
    public string TotalDuration { get; set; } = string.Empty;
    public List<SyncDatabaseResultDto> Databases { get; set; } = new();
    public string Message { get; set; } = string.Empty;
}

public interface ICloudSyncService
{
    Task<bool> IsCloudAvailableAsync(CancellationToken cancellationToken = default);
    Task QueueAndSyncRecordAsync(string tableName, int? companyId, string recordKey, string operation, object payload, CancellationToken cancellationToken = default);
    Task<SyncResult> ProcessPendingSyncQueueAsync(CancellationToken cancellationToken = default);
    Task<SyncResult> RunBaselineSyncAsync(CancellationToken cancellationToken = default);
    Task<CloudSyncStatusDto> GetSyncStatusAsync(CancellationToken cancellationToken = default);

    // Multi-database safe sync contracts
    Task<List<SyncDatabasePreviewDto>> GenerateSyncPreviewAsync(string target, CancellationToken cancellationToken = default);
    Task<MultiSyncResultDto> ExecuteSyncAsync(string target, CancellationToken cancellationToken = default);
    Task<Dictionary<string, bool>> CheckAllCloudDatabasesOnlineAsync(CancellationToken cancellationToken = default);
}
