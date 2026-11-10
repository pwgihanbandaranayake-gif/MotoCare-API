using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;
using MotoCare.Application.Abstractions;
using MotoCare.Application.Dtos;

namespace MotoCare.Infrastructure.Messaging;

/// <summary>Added: sends e-mail with a PDF attachment through any SMTP server (Gmail, Outlook, cPanel ...).</summary>
public sealed class SmtpEmailSender(IOptions<MessagingOptions> options) : IEmailSender
{
    private EmailOptions O => options.Value.Email;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(O.Host) && !string.IsNullOrWhiteSpace(O.FromAddress);

    public async Task SendAsync(string to, string toName, string subject, string body, PdfFile attachment)
    {
        if (!IsConfigured) throw new InvalidOperationException("E-mail is not set up (Messaging:Email in appsettings.json).");

        using var message = new MailMessage
        {
            From = new MailAddress(O.FromAddress, O.FromName),
            Subject = subject,
            Body = body,
        };
        message.To.Add(new MailAddress(to, toName));
        using var stream = new MemoryStream(attachment.Content);
        message.Attachments.Add(new Attachment(stream, attachment.FileName, "application/pdf"));

        using var client = new SmtpClient(O.Host, O.Port)
        {
            EnableSsl = O.UseSsl,
            Credentials = string.IsNullOrWhiteSpace(O.Username) ? null : new NetworkCredential(O.Username, O.Password),
        };
        await client.SendMailAsync(message);
    }
}
