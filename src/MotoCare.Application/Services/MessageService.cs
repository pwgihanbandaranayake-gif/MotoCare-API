using System.Text.RegularExpressions;
using MotoCare.Application.Abstractions;
using MotoCare.Application.Common;
using MotoCare.Application.Dtos;

namespace MotoCare.Application.Services;

/// <summary>
/// Added: sending invoices, payslips and quotations as PDF by e-mail / WhatsApp.
///   - Every message goes into the outbox (MessageLog) first.
///   - Auto send: settings AutoSendInvoice / AutoSendPayslip = 1 -> queued using the customer's / employee's "send via".
///   - The background sender (MessageDispatcher) sends pending messages every 30 seconds; "Send now" sends at once.
/// </summary>
public sealed class MessageService(IMessageRepository repo, ISettingsRepository settings, IDocumentPdfBuilder pdf,
                                   IEmailSender email, IWhatsAppSender whatsApp, ICurrentUser me)
{
    public static readonly string[] DocTypes = { "INVOICE", "PAYSLIP", "QUOTATION" };

    public async Task<RowData> ContactAsync(string docType, int docId)
    {
        string type = DocType(docType);
        return Check.Found(await repo.ContactAsync(type, docId), "Document");
    }

    /// <summary>Queues a document by the contact's preference if auto-send is switched on. Never throws (a bill must not fail because of a message).</summary>
    public async Task AutoSendAsync(string docType, int docId)
    {
        try
        {
            var s = await SettingsReader.ReadAsync(settings);
            bool on = docType == "INVOICE" ? s.Flag("AutoSendInvoice") : docType == "PAYSLIP" && s.Flag("AutoSendPayslip");
            if (on) await QueueByPreferenceAsync(docType, docId);
        }
        catch { /* the outbox screen shows anything that could not be queued */ }
    }

    /// <summary>Queues e-mail and / or WhatsApp as chosen on the customer / employee record. Returns how many were queued.</summary>
    public async Task<int> QueueByPreferenceAsync(string docType, int docId)
    {
        var c = await ContactAsync(docType, docId);
        string via = c.Get<string>("SendVia") ?? "None";
        int queued = 0;
        if (via is "Email" or "Both" && IsEmail(c.Get<string?>("Email")))
        { await Queue(docType, docId, c, "Email", c.Get<string>("Email")); queued++; }
        if (via is "WhatsApp" or "Both" && WhatsAppNumber(c.Get<string?>("Mobile")) is not null)
        { await Queue(docType, docId, c, "WhatsApp", c.Get<string>("Mobile")); queued++; }
        return queued;
    }

    /// <summary>"Send" button: queue the chosen channels and send them straight away.</summary>
    public async Task<object> SendNowAsync(SendDocumentDto dto)
    {
        string type = DocType(dto.DocType);
        Check.That(dto.Email || dto.WhatsApp, "Choose e-mail and / or WhatsApp.");
        var c = await ContactAsync(type, dto.DocId);
        var ids = new List<int>();

        if (dto.Email)
        {
            Check.That(email.IsConfigured, "E-mail is not set up. Fill Messaging:Email in the API appsettings.json.");
            string to = string.IsNullOrWhiteSpace(dto.EmailTo) ? c.Get<string?>("Email") ?? "" : dto.EmailTo.Trim();
            Check.That(IsEmail(to), "Enter a valid e-mail address.");
            ids.Add(await Queue(type, dto.DocId, c, "Email", to));
        }
        if (dto.WhatsApp)
        {
            Check.That(whatsApp.IsConfigured, "WhatsApp is not set up. Fill Messaging:WhatsApp in the API appsettings.json, or use 'Open WhatsApp' to send by hand.");
            string to = string.IsNullOrWhiteSpace(dto.MobileTo) ? c.Get<string?>("Mobile") ?? "" : dto.MobileTo.Trim();
            Check.That(WhatsAppNumber(to) is not null, "Enter a valid mobile number, e.g. 0771234567.");
            ids.Add(await Queue(type, dto.DocId, c, "WhatsApp", to));
        }

        var results = await ProcessAsync((await repo.PendingAsync(200)).Where(m => ids.Contains(m.Get<int>("MessageId"))).ToList());
        return new { sent = results.Count(r => r.ok), failed = results.Where(r => !r.ok).Select(r => r.error) };
    }

    public Task RetryAsync(int messageId) => repo.RetryAsync(messageId);

    /// <summary>Called by the background sender.</summary>
    public async Task ProcessPendingAsync(int max = 20) => await ProcessAsync(await repo.PendingAsync(max));

    // =================================================================== sending
    private async Task<List<(bool ok, string? error)>> ProcessAsync(List<RowData> messages)
    {
        var results = new List<(bool, string?)>();
        var files = new Dictionary<string, PdfFile>();   // build each PDF once
        foreach (var m in messages)
        {
            int id = m.Get<int>("MessageId");
            try
            {
                string type = m.Get<string>("DocType");
                int docId = m.Get<int>("DocId");
                string key = $"{type}:{docId}";
                if (!files.TryGetValue(key, out var file)) files[key] = file = await pdf.BuildAsync(type, docId);

                string name = m.Get<string?>("RecipientName") ?? "Customer";
                string text = $"Dear {name}, please find your {file.Title} attached. Thank you.";
                if (m.Get<string>("Channel") == "Email")
                    await email.SendAsync(m.Get<string>("Recipient"), name, file.Title, text, file);
                else
                    await whatsApp.SendDocumentAsync(WhatsAppNumber(m.Get<string>("Recipient"))!, name, text, file);

                await repo.SetResultAsync(id, "Sent", null);
                results.Add((true, null));
            }
            catch (Exception ex)
            {
                // try again later, up to 3 attempts in total
                string status = m.Get<int>("Attempts") + 1 >= 3 ? "Failed" : "Pending";
                string error = ex.Message.Length > 480 ? ex.Message[..480] : ex.Message;
                await repo.SetResultAsync(id, status, error);
                results.Add((false, error));
            }
        }
        return results;
    }

    private Task<int> Queue(string docType, int docId, RowData contact, string channel, string recipient) =>
        repo.QueueAsync(docType, docId, contact.Get<string?>("DocNo"), channel, recipient.Trim(), contact.Get<string?>("Name"),
                        me.UserId > 0 ? me.UserId : null);

    // =================================================================== helpers
    private static string DocType(string? docType)
    {
        string type = (docType ?? "").Trim().ToUpperInvariant();
        Check.That(DocTypes.Contains(type), "Document type must be INVOICE, PAYSLIP or QUOTATION.");
        return type;
    }

    public static bool IsEmail(string? s) => !string.IsNullOrWhiteSpace(s) && Regex.IsMatch(s.Trim(), @"^[^\s@]+@[^\s@]+\.[^\s@]+$");

    /// <summary>Sri Lankan number for WhatsApp: 0771234567 -> 94771234567. Returns null when not valid.</summary>
    public static string? WhatsAppNumber(string? mobile)
    {
        string digits = Regex.Replace(mobile ?? "", @"\D", "");
        if (digits.Length == 10 && digits.StartsWith("0")) return "94" + digits[1..];
        if (digits.Length == 11 && digits.StartsWith("94")) return digits;
        return null;
    }
}
