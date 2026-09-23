using System;
using CarwashServices.Dtos;
namespace CarwashServices.Dtos
{
    // ============================================================
    // Follow-up DTOs
    //   FollowUpDto       – one row from api/follow-ups
    //   FollowUpStatsDto  – the 4 KPI tiles on the Follow-Ups page
    // ============================================================

    public class FollowUpDto
    {
        public int FollowUpId { get; set; }
        public int CustomerId { get; set; }
        public string Type { get; set; } = "Service Reminder";
        public string ContactMethod { get; set; } = "SMS";
        public string? Reason { get; set; }
        public string? DiscountOffer { get; set; }
        public string? Notes { get; set; }
        public string Status { get; set; } = "Pending";
        public DateTime ScheduledDate { get; set; }
        public DateTime? ValidUntil { get; set; }
        public DateTime? SentAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public int? CreatedBy { get; set; }

        // Archive fields
        public bool IsArchived { get; set; }
        public DateTime? ArchivedAt { get; set; }
        public string? ArchivedBy { get; set; }
    }

    public class FollowUpStatsDto
    {
        public int DueToday { get; set; }
        public int OffersSent { get; set; }
        public int Redeemed { get; set; }
        public int Expired { get; set; }
    }
}