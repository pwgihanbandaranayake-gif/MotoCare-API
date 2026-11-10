using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MotoCare.Api.Infrastructure;
using MotoCare.Application.Abstractions;
using MotoCare.Application.Services;
using MotoCare.Domain.Constants;

namespace MotoCare.Api.Controllers;

/// <summary>
/// Added: system name and company logo.
///   GET  /api/branding        - name, company and logo version (no login needed: used on the login screen)
///   GET  /api/branding/logo   - the logo image (no login needed: used by <img> on screens and printouts)
///   POST /api/branding/logo   - upload a new logo (Settings > Edit). Also used by the PDF bills.
/// The logo file is Assets/logo.png next to the API.
/// </summary>
[Route("api/branding")]
public sealed class BrandingController(ISettingsRepository settings) : ApiControllerBase
{
    private static string LogoPath => Path.Combine(AppContext.BaseDirectory, "Assets", "logo.png");
    private const long MaxBytes = 1024 * 1024;   // 1 MB

    [AllowAnonymous, HttpGet]
    public async Task<IActionResult> Get()
    {
        var s = await SettingsReader.ReadAsync(settings);
        return Ok(new
        {
            systemName = s.Text("SystemName", "MotoCare"),
            companyName = s.Text("CompanyName", "Moto GT"),
            logoVersion = s.Text("LogoVersion", "1"),
            hasLogo = System.IO.File.Exists(LogoPath)
        });
    }

    [AllowAnonymous, HttpGet("logo")]
    public IActionResult Logo()
    {
        if (!System.IO.File.Exists(LogoPath)) return NotFound();
        Response.Headers.CacheControl = "no-cache";
        return PhysicalFile(LogoPath, "image/png");
    }

    [HttpPost("logo"), Permission(AppModules.Settings, PermissionActions.Edit)]
    [RequestSizeLimit(2 * 1024 * 1024)]
    public async Task<IActionResult> Upload(IFormFile? file)
    {
        // ---- checks: PNG or JPG, up to 1 MB
        if (file is null || file.Length == 0) return BadRequest(new { message = "Choose an image file." });
        if (file.Length > MaxBytes) return BadRequest(new { message = "The logo must be 1 MB or smaller." });
        string ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext is not (".png" or ".jpg" or ".jpeg") || !file.ContentType.StartsWith("image/"))
            return BadRequest(new { message = "The logo must be a PNG or JPG image." });

        // ---- save (the file name stays logo.png; browsers and PDF read it from there)
        Directory.CreateDirectory(Path.GetDirectoryName(LogoPath)!);
        await using (var stream = System.IO.File.Create(LogoPath))
            await file.CopyToAsync(stream);

        // ---- new version number so screens load the new image instead of a cached one
        var s = await SettingsReader.ReadAsync(settings);
        int version = int.TryParse(s.Text("LogoVersion", "1"), out var v) ? v + 1 : 2;
        await settings.SaveAsync("LogoVersion", version.ToString());
        return Ok(new { logoVersion = version.ToString() });
    }
}
