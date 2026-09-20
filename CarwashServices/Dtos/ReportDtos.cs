using System.Collections.Generic;
using CarwashServices.Dtos;
namespace CarwashServices.Dtos
{
    // ============================================================
    // Reports DTOs (used by ReportsView)
    // ============================================================

    public class ReportsResponseDto
    {
        public int TotalTransactions { get; set; }
        public int Completed { get; set; }
        public int PendingOrCancelled { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal AvgTicket { get; set; }
        public List<ReportMonthPointDto> Months { get; set; } = new();
        public List<ReportServicePointDto> ByService { get; set; } = new();
        public List<ReportTxnDto> Transactions { get; set; } = new();
        public List<string> ServiceOptions { get; set; } = new();
        public List<string> VehicleOptions { get; set; } = new();
        public string GeneratedAt { get; set; } = "";
    }

    public class ReportMonthPointDto
    {
        public string Label { get; set; } = "";
        public decimal Value { get; set; }
    }

    public class ReportServicePointDto
    {
        public string Label { get; set; } = "";
        public decimal Value { get; set; }
    }

    public class ReportTxnDto
    {
        public string Txn { get; set; } = "";
        public string Date { get; set; } = "";
        public string Customer { get; set; } = "";
        public string Vehicle { get; set; } = "";
        public string Service { get; set; } = "";
        public decimal Amount { get; set; }
        public string Payment { get; set; } = "";
        public string Status { get; set; } = "";
    }
}