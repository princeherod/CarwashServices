using System.Collections.Generic;

namespace CarwashServices.Dtos
{
    // Complaints & Feedback
    public class ComplaintsFeedbackReportDto
    {
        public int TotalFeedback { get; set; }
        public int TotalComplaints { get; set; }
        public int ResolvedComplaints { get; set; }
        public double ResolutionRate { get; set; }
        public double AvgRating { get; set; }
        public SentimentDto Sentiment { get; set; } = new();
        public List<NamedValueDto> ComplaintsByCategory { get; set; } = new();
        public List<ComplaintFeedbackRowDto> Rows { get; set; } = new();
        public string GeneratedAt { get; set; } = "";
    }

    public class SentimentDto
    {
        public int Positive { get; set; }
        public int Neutral { get; set; }
        public int Negative { get; set; }
        public int Total { get; set; }
    }

    public class NamedValueDto
    {
        public string Label { get; set; } = "";
        public decimal Value { get; set; }
    }

    public class ComplaintFeedbackRowDto
    {
        public string Type { get; set; } = "";
        public string Date { get; set; } = "";
        public string Customer { get; set; } = "";
        public string Details { get; set; } = "";
        public string RatingOrCategory { get; set; } = "";
        public string Status { get; set; } = "";
    }

    // Customer Activity
    public class CustomerActivityReportDto
    {
        public int TotalCustomers { get; set; }
        public int ActiveCustomers { get; set; }
        public double ReturningRate { get; set; }
        public double AvgServicesPerCustomer { get; set; }
        public List<NamedValueDto> BySource { get; set; } = new();
        public List<NamedValueDto> ByVehicle { get; set; } = new();
        public List<CustomerActivityRowDto> Rows { get; set; } = new();
        public string GeneratedAt { get; set; } = "";
    }

    public class CustomerActivityRowDto
    {
        public string Customer { get; set; } = "";
        public string Phone { get; set; } = "";
        public string VehicleType { get; set; } = "";
        public string Source { get; set; } = "";
        public int TotalVisits { get; set; }
        public string LastVisit { get; set; } = "";
        public decimal LifetimeValue { get; set; }
        public decimal AvgSpend { get; set; }
        public string Status { get; set; } = "";
    }

    // Retention Summary
    public class RetentionSummaryReportDto
    {
        public double RetentionRate { get; set; }
        public int AtRisk { get; set; }
        public int Churned { get; set; }
        public int Unresolved { get; set; }
        public RetentionSegmentationDto Segmentation { get; set; } = new();
        public List<NamedValueDto> TopByLtv { get; set; } = new();
        public List<RetentionSummaryRowDto> Rows { get; set; } = new();
        public string GeneratedAt { get; set; } = "";
    }

    public class RetentionSegmentationDto
    {
        public int Healthy { get; set; }
        public int AtRisk { get; set; }
        public int Churned { get; set; }
        public int Total { get; set; }
    }

    public class RetentionSummaryRowDto
    {
        public int CustomerId { get; set; }
        public string Customer { get; set; } = "";
        public string LastVisit { get; set; } = "";
        public string DaysSince { get; set; } = "";
        public int TotalVisits { get; set; }
        public decimal Ltv { get; set; }
        public string AvgRating { get; set; } = "";
        public string Complaint { get; set; } = "";
        public string Segment { get; set; } = "";
        public string RetentionAction { get; set; } = "";
    }
}