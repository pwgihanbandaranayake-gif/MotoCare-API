namespace MotoCare.Infrastructure.Messaging;

/// <summary>
/// Added: "Messaging" section of appsettings.json. Leave a part empty to switch that channel off.
/// Passwords / tokens live in appsettings (not in the database) so they are never shown in the app.
/// </summary>
public sealed class MessagingOptions
{
    public EmailOptions Email { get; set; } = new();
    public WhatsAppOptions WhatsApp { get; set; } = new();
}

public sealed class EmailOptions
{
    public string Host { get; set; } = "";          // e.g. smtp.gmail.com
    public int Port { get; set; } = 587;
    public bool UseSsl { get; set; } = true;
    public string Username { get; set; } = "";      // e.g. billing@motogt.lk
    public string Password { get; set; } = "";      // Gmail: use an "App password"
    public string FromAddress { get; set; } = "";
    public string FromName { get; set; } = "Moto GT";
}

public sealed class WhatsAppOptions
{
    // WhatsApp Business Cloud API (Meta). See README "WhatsApp setup".
    public string AccessToken { get; set; } = "";
    public string PhoneNumberId { get; set; } = "";
    public string ApiVersion { get; set; } = "v20.0";
    // Approved template with a DOCUMENT header and one body variable {{1}} = customer name.
    // Needed to message people who have not written to you in the last 24 hours.
    public string TemplateName { get; set; } = "";
    public string TemplateLanguage { get; set; } = "en";
}
