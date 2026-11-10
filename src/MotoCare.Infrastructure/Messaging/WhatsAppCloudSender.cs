using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using MotoCare.Application.Abstractions;
using MotoCare.Application.Dtos;

namespace MotoCare.Infrastructure.Messaging;

/// <summary>
/// Added: sends a PDF through the WhatsApp Business Cloud API.
///   1. upload the PDF  -> media id
///   2. send a message  -> template with document header (if TemplateName is set), otherwise a plain document message
///      (plain messages only reach people who messaged your business number in the last 24 hours).
/// </summary>
public sealed class WhatsAppCloudSender(IHttpClientFactory httpFactory, IOptions<MessagingOptions> options) : IWhatsAppSender
{
    private WhatsAppOptions O => options.Value.WhatsApp;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(O.AccessToken) && !string.IsNullOrWhiteSpace(O.PhoneNumberId);

    public async Task SendDocumentAsync(string mobile, string recipientName, string caption, PdfFile document)
    {
        if (!IsConfigured) throw new InvalidOperationException("WhatsApp is not set up (Messaging:WhatsApp in appsettings.json).");

        var http = httpFactory.CreateClient("whatsapp");
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", O.AccessToken);
        string baseUrl = $"https://graph.facebook.com/{O.ApiVersion}/{O.PhoneNumberId}";

        // ---- 1. upload the PDF
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent("whatsapp"), "messaging_product");
        form.Add(new StringContent("application/pdf"), "type");
        var file = new ByteArrayContent(document.Content);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(file, "file", document.FileName);

        var upload = await http.PostAsync($"{baseUrl}/media", form);
        string uploadBody = await upload.Content.ReadAsStringAsync();
        if (!upload.IsSuccessStatusCode) throw new InvalidOperationException($"WhatsApp upload failed: {uploadBody}");
        string mediaId = JsonDocument.Parse(uploadBody).RootElement.GetProperty("id").GetString()!;

        // ---- 2. send the message
        object payload = string.IsNullOrWhiteSpace(O.TemplateName)
            ? (object)new
            {
                messaging_product = "whatsapp", to = mobile, type = "document",
                document = new { id = mediaId, filename = document.FileName, caption }
            }
            : new
            {
                messaging_product = "whatsapp", to = mobile, type = "template",
                template = new
                {
                    name = O.TemplateName,
                    language = new { code = O.TemplateLanguage },
                    components = new object[]
                    {
                        new { type = "header", parameters = new object[] { new { type = "document", document = new { id = mediaId, filename = document.FileName } } } },
                        new { type = "body", parameters = new object[] { new { type = "text", text = recipientName } } }
                    }
                }
            };

        var send = await http.PostAsJsonAsync($"{baseUrl}/messages", payload);
        if (!send.IsSuccessStatusCode) throw new InvalidOperationException($"WhatsApp send failed: {await send.Content.ReadAsStringAsync()}");
    }
}
