using System.Net;
using System.Net.Mail;

using Microsoft.Extensions.Options;

namespace CRM.api.Services;

public class SmtpEmailSender : IEmailSender
{
    private readonly SmtpOptions _options;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(IOptions<SmtpOptions> options, ILogger<SmtpEmailSender> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<EmailSendResult> SendAsync(
        string toEmail,
        string toName,
        string subject,
        string htmlBody,
        CancellationToken ct = default)
    {
        if (!_options.IsConfigured)
        {
            _logger.LogWarning("SMTP is not configured. Email to {To} was not sent.", toEmail);
            return EmailSendResult.Fail(
                "The email server is not configured. Contact your administrator.");
        }

        if (string.IsNullOrWhiteSpace(toEmail) || !IsValidEmail(toEmail))
        {
            return EmailSendResult.Fail(
                "The customer does not have a valid email address on file.");
        }

        try
        {
            using var message = new MailMessage
            {
                From = new MailAddress(_options.SenderEmail, _options.SenderName),
                Subject = subject,
                Body = htmlBody,
                IsBodyHtml = true,
                SubjectEncoding = System.Text.Encoding.UTF8,
                BodyEncoding = System.Text.Encoding.UTF8
            };

            message.To.Add(new MailAddress(toEmail, toName));

            using var client = new SmtpClient(_options.Host, _options.Port)
            {
                EnableSsl = _options.UseStartTls,
                Credentials = new NetworkCredential(_options.Username, _options.Password),
                DeliveryMethod = SmtpDeliveryMethod.Network
            };

            await client.SendMailAsync(message, ct);
            return EmailSendResult.Ok();
        }
        catch (SmtpException smtpEx)
        {
            _logger.LogError(smtpEx,
                "SMTP send failed for {To}. Status: {Status}", toEmail, smtpEx.StatusCode);
            return EmailSendResult.Fail(
                $"The email could not be sent: {smtpEx.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error sending email to {To}.", toEmail);
            return EmailSendResult.Fail(
                "An unexpected error occurred while sending the email.");
        }
    }

    /// <summary>
    /// Lightweight format check — enough to catch typos, not a mailbox verifier.
    /// </summary>
    private static bool IsValidEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email)) return false;
        try
        {
            var addr = new MailAddress(email);
            return string.Equals(addr.Address, email, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }
}