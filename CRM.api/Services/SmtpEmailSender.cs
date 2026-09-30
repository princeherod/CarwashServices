using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using MimeKit.Utils;
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
            return EmailSendResult.Fail("The email server is not configured. Contact your administrator.");
        }

        if (string.IsNullOrWhiteSpace(toEmail) || !IsValidEmail(toEmail))
        {
            _logger.LogWarning("Invalid recipient email: '{To}'", toEmail);
            return EmailSendResult.Fail("The customer does not have a valid email address on file.");
        }

        try
        {
            var message = new MimeMessage();

            var senderName = CleanDisplayName(_options.SenderName);
            var senderAddress = _options.SenderEmail.Trim();
            message.From.Add(new MailboxAddress(senderName, senderAddress));

            var recipientName = CleanDisplayName(toName);
            if (string.Equals(recipientName, toEmail.Trim(), StringComparison.OrdinalIgnoreCase) || recipientName.Contains('@'))
            {
                recipientName = "";
            }
            message.To.Add(new MailboxAddress(recipientName, toEmail.Trim()));

            message.Subject = subject ?? string.Empty;

            // Generate RFC 5322 compliant Message-ID with the sender's domain (e.g. gmail.com)
            // This is critical for Google SMTP external relay so messages to external domains aren't flagged or dropped.
            var domain = GetDomainFromEmail(senderAddress);
            message.MessageId = MimeUtils.GenerateMessageId(domain);

            // Add standard headers
            message.ReplyTo.Add(new MailboxAddress(senderName, senderAddress));
            message.Headers.Add("X-Mailer", "AquaShine-CRM-Mailer");

            var bodyBuilder = new BodyBuilder
            {
                HtmlBody = htmlBody,
                TextBody = HtmlToPlainText(htmlBody)
            };
            message.Body = bodyBuilder.ToMessageBody();

            using var client = new SmtpClient();

            // Timeout in milliseconds
            client.Timeout = 20000;

            // Determine appropriate SSL/TLS option
            SecureSocketOptions socketOptions = _options.Port switch
            {
                465 => SecureSocketOptions.SslOnConnect,
                587 => SecureSocketOptions.StartTls,
                25 => SecureSocketOptions.StartTlsWhenAvailable,
                _ => _options.UseStartTls ? SecureSocketOptions.StartTls : SecureSocketOptions.Auto
            };

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(25));

            _logger.LogInformation("Connecting to SMTP host {Host}:{Port} using {Security}...",
                _options.Host, _options.Port, socketOptions);

            await client.ConnectAsync(_options.Host, _options.Port, socketOptions, cts.Token);

            _logger.LogInformation("Authenticating SMTP with username {Username}...", _options.Username);
            await client.AuthenticateAsync(_options.Username, _options.Password, cts.Token);

            _logger.LogInformation("Sending email to {To} with Subject '{Subject}'...", toEmail, subject);
            await client.SendAsync(message, cts.Token);

            // Graceful QUIT handshake to ensure MTA commits relay to external mail servers
            await client.DisconnectAsync(true, cts.Token);

            _logger.LogInformation("Email sent successfully to {To}", toEmail);
            return EmailSendResult.Ok();
        }
        catch (SmtpCommandException cmdEx)
        {
            _logger.LogError(cmdEx,
                "SMTP command error sending to {To}. Status: {Code}, Message: {Message}",
                toEmail, cmdEx.StatusCode, cmdEx.Message);
            return EmailSendResult.Fail($"Mail server error ({cmdEx.StatusCode}): {cmdEx.Message}");
        }
        catch (AuthenticationException authEx)
        {
            _logger.LogError(authEx, "SMTP authentication failed for {Username}.", _options.Username);
            return EmailSendResult.Fail("SMTP authentication failed. Please verify your Google App Password.");
        }
        catch (OperationCanceledException)
        {
            _logger.LogError("SMTP send timed out for {To}.", toEmail);
            return EmailSendResult.Fail("The email request timed out while connecting to the mail server.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error sending email to {To}: {Message}", toEmail, ex.Message);
            return EmailSendResult.Fail($"An error occurred while sending the email: {ex.Message}");
        }
    }

    private static string GetDomainFromEmail(string email)
    {
        var at = email.IndexOf('@');
        if (at > 0 && at < email.Length - 1)
        {
            return email.Substring(at + 1).Trim();
        }
        return "gmail.com";
    }

    private static string CleanDisplayName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "";
        return name.Replace("\r", "").Replace("\n", "").Trim();
    }

    private static bool IsValidEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email)) return false;
        try
        {
            return MailboxAddress.TryParse(email, out _);
        }
        catch
        {
            return false;
        }
    }

    private static string HtmlToPlainText(string? html)
    {
        if (string.IsNullOrWhiteSpace(html)) return string.Empty;
        var text = System.Text.RegularExpressions.Regex.Replace(html, @"<style[^>]*>[\s\S]*?</style>", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        text = System.Text.RegularExpressions.Regex.Replace(text, @"<br\s*/?>|</p>|</div>|</tr>", "\n", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        text = System.Text.RegularExpressions.Regex.Replace(text, @"<[^>]+>", " ");
        text = System.Net.WebUtility.HtmlDecode(text);
        text = System.Text.RegularExpressions.Regex.Replace(text, @"[ \t]+", " ");
        text = System.Text.RegularExpressions.Regex.Replace(text, @"\n\s*\n+", "\n\n");
        return text.Trim();
    }
}