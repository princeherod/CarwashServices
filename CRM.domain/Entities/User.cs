using System;
using System.Collections.Generic;

namespace CRM.domain.Entities
{
    public class User
    {
        public int UserId { get; set; }
        public int RoleId { get; set; }
        public string IdentityUserId { get; set; } // FK to AspNetUsers.Id (nullable until account is linked/activated)
        public string FullName { get; set; }
        public string Email { get; set; }
        public string PasswordHash { get; set; }
        public string Status { get; set; } // Active, Inactive
        public DateTime CreatedAt { get; set; }

        public Role Role { get; set; }

        public ICollection<ServiceRequest> AssignedRequests { get; set; } = new List<ServiceRequest>();
        public ICollection<ServiceRequest> CreatedRequests { get; set; } = new List<ServiceRequest>();
        public ICollection<ServiceStatusLog> StatusUpdates { get; set; } = new List<ServiceStatusLog>();
        public ICollection<FollowUp> FollowUpsCreated { get; set; } = new List<FollowUp>();
        public ICollection<CustomerSubscription> ManagedSubscriptions { get; set; } = new List<CustomerSubscription>();
        public ICollection<BackupLog> BackupsPerformed { get; set; } = new List<BackupLog>();
        public ICollection<TermsCondition> TermsPublished { get; set; } = new List<TermsCondition>();
    }
}