using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Pliaforma.Api.RateLimiting;

namespace Pliaforma.Api.Controllers;

/// <summary>
/// Sprint 31C-2 — CSP ihlal raporu toplayıcı (report-uri hedefi). <b>Salt-log</b>: DB/SecurityAuditLog'a YAZMAZ.
/// AllowAnonymous (tarayıcı gönderir), IP rate-limit + body size limit. Gövde sanitize/truncate edilip ILogger'a
/// yazılır; querystring/fragment/token/secret loglanmaz. Her durumda <b>204</b> döner (crash yok).
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/csp-report")]
[EnableRateLimiting(RateLimitPolicies.CspReport)]
public sealed class CspReportController : ControllerBase
{
    private const int MaxBytes = 8 * 1024;   // 8KB üstü → 413 (RequestSizeLimit)
    private const int FieldMax = 256;        // log alanı başına üst sınır
    private static readonly Regex TokenLike = new(@"[A-Za-z0-9_\-]{40,}", RegexOptions.Compiled);

    private readonly ILogger<CspReportController> _logger;
    public CspReportController(ILogger<CspReportController> logger) => _logger = logger;

    [HttpPost]
    [RequestSizeLimit(MaxBytes)]
    public async Task<IActionResult> Report(CancellationToken ct)
    {
        // Oversize → 413. (Gövdeyi cap'li okuduğumuz için RequestSizeLimit tek başına tetiklenmez; Content-Length kontrolü kesin.)
        if (Request.ContentLength is > MaxBytes)
            return StatusCode(StatusCodes.Status413PayloadTooLarge);

        // Ham gövdeyi cap'li oku (model binder'a güvenme; arbitrary content-type). Hata olsa da 204.
        string raw;
        try
        {
            using var reader = new StreamReader(Request.Body, Encoding.UTF8, leaveOpen: true);
            var buf = new char[MaxBytes];
            var n = await reader.ReadBlockAsync(buf.AsMemory(0, MaxBytes), ct);
            raw = new string(buf, 0, n);
        }
        catch
        {
            return NoContent();
        }

        var (directive, blocked, document) = Parse(raw);
        // Yalnız sanitize edilmiş, minimal alanlar. Ham gövde / querystring / token loglanmaz.
        _logger.LogWarning("CSP violation report: directive={Directive} blocked={Blocked} document={Document}",
            directive, blocked, document);

        return NoContent();   // 204
    }

    /// <summary>report-uri ({"csp-report":{…}}) ve report-to ([{"body":{…}}]) gövdelerini best-effort ayrıştırır.</summary>
    private static (string Directive, string Blocked, string Document) Parse(string raw)
    {
        var directive = "(unknown)"; var blocked = "-"; var document = "-";
        try
        {
            using var json = JsonDocument.Parse(raw);
            var root = json.RootElement;
            JsonElement report = root;
            if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("csp-report", out var cr))
                report = cr;                                   // report-uri klasik
            else if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0
                     && root[0].TryGetProperty("body", out var body))
                report = body;                                 // report-to (reports+json)

            directive = Field(report, "violated-directive", "effective-directive", "effectiveDirective");
            blocked = Field(report, "blocked-uri", "blockedURL");
            document = Field(report, "document-uri", "documentURL");
        }
        catch
        {
            // Geçersiz/parse edilemeyen gövde → varsayılanlar, crash yok.
        }
        return (CleanDirective(directive), SanitizeUri(blocked, pathOnly: false), SanitizeUri(document, pathOnly: true));
    }

    private static string Field(JsonElement el, params string[] names)
    {
        foreach (var n in names)
            if (el.ValueKind == JsonValueKind.Object && el.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.String)
                return v.GetString() ?? "-";
        return "-";
    }

    /// <summary>Direktif adını sadeleştirir (ör. "script-src-elem 'nonce-…'" → "script-src-elem").</summary>
    private static string CleanDirective(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return "(unknown)";
        var first = s.Trim().Split(' ', 2)[0];
        return Redact(first, 64);
    }

    /// <summary>URI'den querystring + fragment + (path-only ise host) atılır; token-benzeri kalıplar redakte edilir.</summary>
    private static string SanitizeUri(string? s, bool pathOnly)
    {
        if (string.IsNullOrWhiteSpace(s)) return "-";
        s = s.Trim();
        // CSP anahtar kelimeleri olduğu gibi (inline/eval/self/data/blob).
        if (s is "inline" or "eval" or "self" or "data" or "blob" || s.StartsWith("data:") || s.StartsWith("blob:"))
            return Redact(s, FieldMax);
        if (Uri.TryCreate(s, UriKind.Absolute, out var uri))
            return Redact(pathOnly ? uri.AbsolutePath : $"{uri.Scheme}://{uri.Host}{uri.AbsolutePath}", FieldMax);
        // Relative / parse edilemeyen → '?' ve '#' öncesini al.
        var cut = s.Split('?', '#')[0];
        return Redact(cut, FieldMax);
    }

    /// <summary>CR/LF temizler, token-benzeri uzun dizileri redakte eder, üst sınıra kırpar.</summary>
    private static string Redact(string s, int max)
    {
        s = s.Replace("\r", " ").Replace("\n", " ");
        s = TokenLike.Replace(s, "[redacted]");
        return s.Length > max ? s[..max] : s;
    }
}
