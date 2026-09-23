using System.Collections.Generic;
using CarwashServices.Dtos;
namespace CarwashServices.Dtos
{
    // ============================================================
    // Analytics DTOs (used by AnalyticsView)
    // ============================================================

    public class AnalyticsSummaryDto
    {
        public int TotalCustomers { get; set; }
        public int ReturningCustomers { get; set; }
        public double ChurnRate { get; set; }
        public decimal AvgSpendPerCustomer { get; set; }
        public int CarsWashedThisMonth { get; set; }
        public decimal RevenueThisMonth { get; set; }
    }

    public class RetentionPointDto
    {
        public string Month { get; set; } = "";
        public int Value { get; set; }
    }

    public class RevenuePointDto
    {
        public string Label { get; set; } = "";
        public decimal Value { get; set; }
    }

    public class RevenueResponseDto
    {
        public List<RevenuePointDto> Months { get; set; } = new();
        public decimal Ytd { get; set; }
        public string BestMonth { get; set; } = "";
        public decimal BestValue { get; set; }
    }

    // NEW — Wash Frequency by Segment
    public class WashFrequencyPointDto
    {
        public string Label { get; set; } = "";        // "New" | "Occasional" | "Regular" | "Loyal"
        public double Value { get; set; }              // avg completed washes / month
        public int CustomerCount { get; set; }         // # customers in the tier
        public string SegmentKey { get; set; } = "";   // key the drill-down uses
    }
}