using System;

namespace CRM.domain.Entities
{
    public class BackupLog
    {
        public int BackupId { get; set; }
        public int PerformedBy { get; set; }
        public string Type { get; set; } = string.Empty; // Backup, Restore
        public DateTime BackupDate { get; set; }
        public string FileLocation { get; set; }
        public string Status { get; set; } // Success, Failed

        public User PerformedByUser { get; set; }
    }
}