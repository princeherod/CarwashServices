using System;

namespace CarwashServices.Dtos
{
    public class BackupItemDto
    {
        public int BackupId { get; set; }
        public int PerformedBy { get; set; }
        public string PerformedByName { get; set; } = string.Empty;
        public string Type { get; set; } = "Manual";
        public DateTime BackupDate { get; set; }
        public string FileLocation { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public long FileSizeBytes { get; set; }
        public string FileSizeFormatted { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public bool FileExists { get; set; }
    }

    public class CreateBackupRequest
    {
        public int? PerformedBy { get; set; }
        public string? Type { get; set; }
    }

    public class CreateBackupResponse
    {
        public string Message { get; set; } = string.Empty;
        public int BackupId { get; set; }
        public string Type { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string FileLocation { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public DateTime BackupDate { get; set; }
    }

    public class RestoreBackupResponse
    {
        public string Message { get; set; } = string.Empty;
        public int BackupId { get; set; }
        public string FileLocation { get; set; } = string.Empty;
        public DateTime RestoredAt { get; set; }
    }
}
