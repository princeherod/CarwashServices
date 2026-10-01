using System;
using System.Collections.Generic;
using System.Linq;

namespace CarwashServices.Dtos
{
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
}
