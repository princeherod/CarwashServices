using System;

namespace CRM.domain.Entities
{
    public class ServiceStatusLog
    {
        public int LogId { get; set; }
        public int RequestId { get; set; }
        public string Status { get; set; }
        public int UpdatedBy { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string Notes { get; set; }

        public ServiceRequest ServiceRequest { get; set; }
        public User UpdatedByUser { get; set; }
    }
}