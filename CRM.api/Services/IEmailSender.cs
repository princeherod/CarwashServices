namespace CRM.api.Services;

public class EmailSendResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }

    public static EmailSendResult Ok() => new() { Success = true };
    public static EmailSendResult Fail(string message) => new() { Success = false, ErrorMessage = message };
}

public interface IEmailSender
{
    /// <summary>
    /// Sends an HTML email. Returns a result — never throws for SMTP failures,
    /// so the caller can decide whether to mark the follow-up as Sent.
    /// </summary>
    Task<EmailSendResult> SendAsync(
        string toEmail,
        string toName,
        string subject,
        string htmlBody,
        CancellationToken ct = default);
}