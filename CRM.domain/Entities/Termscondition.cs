using System;

namespace CRM.domain.Entities
{
    public class TermsCondition
    {
        public int TermsId { get; set; }
        public string Version { get; set; }
        public string Content { get; set; }
        public DateTime EffectiveDate { get; set; }
        public int CreatedBy { get; set; }

        public User CreatedByUser { get; set; }
    }
}