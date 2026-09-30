using System;

namespace CarwashServices.Dtos
{
    public class TermsItemDto
    {
        public int TermsId { get; set; }
        public string Version { get; set; } = string.Empty;
        public DateTime EffectiveDate { get; set; }
        public string EffectiveDateFormatted { get; set; } = string.Empty;
        public int CreatedBy { get; set; }
        public string CreatedByName { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
    }

    public class CreateTermsRequest
    {
        public string? Content { get; set; }
        public int? CreatedBy { get; set; }
        public string? Version { get; set; }
    }

    public class AcceptTermsRequest
    {
        public int CompanyId { get; set; }
        public int UserId { get; set; }
        public int? TermsId { get; set; }
        public string? Version { get; set; }
    }

    public class AcceptTermsResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public int CompanyId { get; set; }
        public bool TermsAccepted { get; set; }
        public DateTime? TermsAcceptedAt { get; set; }
        public string? TermsAcceptedBy { get; set; }
        public string? TermsAcceptedVersion { get; set; }
    }

    public class DeclineTermsRequest
    {
        public int CompanyId { get; set; }
        public int UserId { get; set; }
        public string? Reason { get; set; }
    }

    public class CompanyTermsStatusDto
    {
        public int CompanyId { get; set; }
        public string CompanyCode { get; set; } = string.Empty;
        public string CompanyName { get; set; } = string.Empty;
        public bool TermsAccepted { get; set; }
        public DateTime? TermsAcceptedAt { get; set; }
        public string? TermsAcceptedBy { get; set; }
        public string? TermsAcceptedVersion { get; set; }
        public string LatestVersion { get; set; } = string.Empty;
        public int? LatestTermsId { get; set; }
        public bool NeedsAcceptance { get; set; }
    }
}
