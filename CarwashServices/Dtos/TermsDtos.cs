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
}
