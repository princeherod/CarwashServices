using System.Net;
using System.Text;

namespace CRM.api.Services;

/// <summary>
/// Builds the customer-facing email for a follow-up.
///
/// Never exposes internal CRM fields (Reason, FollowUpId, CreatedBy, role).
/// Uses only what the customer should see: their name, the offer, the
/// expiry, the message preview, and the business name.
/// </summary>
public static class FollowUpEmailTemplate
{
    public static string BuildSubject(string businessName)
        => $"Your Special Offer from {businessName}";

    public static string BuildHtml(
        string customerName,
        string messagePreview,
        string? discountOffer,
        DateTime? validUntil,
        string businessName)
    {
        // Fallbacks so a missing field never renders as a placeholder in the inbox.
        var safeName = string.IsNullOrWhiteSpace(customerName) ? "there" : customerName.Trim();
        var safeMessage = string.IsNullOrWhiteSpace(messagePreview)
            ? $"We'd love to welcome you back to {businessName}."
            : messagePreview.Trim();
        var safeBusiness = string.IsNullOrWhiteSpace(businessName) ? "AquaShine Car Wash" : businessName.Trim();

        bool hasOffer = !string.IsNullOrWhiteSpace(discountOffer) && validUntil.HasValue;

        var sb = new StringBuilder();
        sb.Append("<!DOCTYPE html><html><head><meta charset=\"utf-8\">");
        sb.Append("<meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">");
        sb.Append("<title>").Append(Html(safeBusiness)).Append("</title></head>");
        sb.Append("<body style=\"margin:0;padding:0;background:#f0f4fa;font-family:'Segoe UI',Helvetica,Arial,sans-serif;color:#0a1633;\">");

        sb.Append("<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" style=\"background:#f0f4fa;\">");
        sb.Append("<tr><td align=\"center\" style=\"padding:32px 12px;\">");

        sb.Append("<table role=\"presentation\" width=\"560\" cellpadding=\"0\" cellspacing=\"0\" style=\"background:#ffffff;border-radius:12px;border:1px solid #e1e7f0;\">");

        // Header
        sb.Append("<tr><td style=\"padding:28px 32px 8px 32px;\">");
        sb.Append("<div style=\"font-size:13px;letter-spacing:1.2px;color:#6b7a9a;text-transform:uppercase;\">")
          .Append(Html(safeBusiness))
          .Append("</div>");
        sb.Append("</td></tr>");

        // Greeting + message
        sb.Append("<tr><td style=\"padding:8px 32px 4px 32px;\">");
        sb.Append("<h1 style=\"margin:0;font-size:22px;color:#0a1633;\">Hi ")
          .Append(Html(safeName))
          .Append(",</h1>");
        sb.Append("</td></tr>");

        sb.Append("<tr><td style=\"padding:12px 32px 20px 32px;font-size:15px;line-height:1.5;color:#4a5a78;\">")
          .Append(Html(safeMessage).Replace("\n", "<br>"))
          .Append("</td></tr>");

        // Offer block — only if the follow-up actually carries an offer
        if (hasOffer)
        {
            sb.Append("<tr><td style=\"padding:0 32px 20px 32px;\">");
            sb.Append("<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" style=\"background:#e3f1fd;border-radius:10px;\">");
            sb.Append("<tr><td align=\"center\" style=\"padding:22px 16px;\">");
            sb.Append("<div style=\"font-size:12px;letter-spacing:1.5px;color:#1565c0;text-transform:uppercase;margin-bottom:6px;\">Special Offer</div>");
            sb.Append("<div style=\"font-size:24px;font-weight:600;color:#0a1633;margin:6px 0;\">")
              .Append(Html(discountOffer!.Trim()))
              .Append("</div>");
            sb.Append("<div style=\"font-size:13px;color:#4a5a78;margin-top:10px;\">Valid until ")
              .Append(Html(validUntil!.Value.ToString("MMMM d, yyyy")))
              .Append("</div>");
            sb.Append("</td></tr></table></td></tr>");
        }

        // Closing
        sb.Append("<tr><td style=\"padding:8px 32px 28px 32px;font-size:15px;line-height:1.5;color:#4a5a78;\">");
        sb.Append("We look forward to seeing you again!");
        sb.Append("</td></tr>");

        // Footer
        sb.Append("<tr><td style=\"padding:18px 32px 24px 32px;border-top:1px solid #e1e7f0;font-size:12px;color:#9aa7bf;\">");
        sb.Append(Html(safeBusiness));
        sb.Append("</td></tr>");

        sb.Append("</table></td></tr></table></body></html>");

        return sb.ToString();
    }

    private static string Html(string s)
        => string.IsNullOrEmpty(s) ? "" : WebUtility.HtmlEncode(s);
}