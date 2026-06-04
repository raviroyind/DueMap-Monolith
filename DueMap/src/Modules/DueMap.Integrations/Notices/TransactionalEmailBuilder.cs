using System.Globalization;
using System.Net;
using System.Text;

namespace DueMap.Integrations.Notices;

/// <summary>
/// Single source of truth for the look-and-feel of every transactional email
/// the platform sends outside the per-PM editable notice templates. PM signup
/// welcome, tenant magic link, preflight tenant intro, future receipts —
/// all flow through here so they share one visual language.
///
/// Output is fully inline-CSS HTML (table-based layout for Outlook desktop)
/// plus a plain-text alternative. Both are returned in a <see cref="BrandedEmail"/>
/// the caller drops straight onto a <see cref="DispatchRequest"/>.
/// </summary>
public static class TransactionalEmailBuilder
{
    // Brand-aligned with the in-app indigo.
    private const string Accent       = "#4f46e5";   // indigo-600
    private const string AccentDark   = "#4338ca";   // indigo-700
    private const string Ink          = "#0f172a";   // slate-900
    private const string Muted        = "#6b7280";   // gray-500
    private const string Border       = "#e5e7eb";   // gray-200
    private const string PageBg       = "#f8fafc";   // slate-50
    private const string CardBg       = "#ffffff";

    public static BrandedEmail Build(TransactionalEmailRequest req)
    {
        ArgumentNullException.ThrowIfNull(req);

        var sender = string.IsNullOrWhiteSpace(req.WorkspaceName) ? "DueMap" : req.WorkspaceName;
        var preheader = req.Preheader ?? StripHtml(req.IntroParagraph);

        var html = new StringBuilder(4096);

        // ---- Boilerplate: doctype + head + body open ------------------------
        html.Append("""
            <!doctype html>
            <html lang="en">
            <head>
              <meta charset="utf-8" />
              <meta name="viewport" content="width=device-width,initial-scale=1" />
              <meta name="x-apple-disable-message-reformatting" />
              <title>
            """);
        html.Append(HtmlEncode(req.Subject));
        html.Append("</title></head>");

        html.Append(CultureInfo.InvariantCulture, $"""
            <body style="margin:0;padding:0;background:{PageBg};color:{Ink};font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,'Helvetica Neue',Arial,sans-serif;-webkit-font-smoothing:antialiased;">
            """);

        // Hidden pre-header — surfaces in inbox previews on most clients.
        html.Append(CultureInfo.InvariantCulture, $"""
            <div style="display:none;font-size:1px;line-height:1px;max-height:0;max-width:0;opacity:0;overflow:hidden;mso-hide:all;">
              {HtmlEncode(preheader)}
            </div>
            """);

        // ---- Outer wrap + centered card ------------------------------------
        html.Append(CultureInfo.InvariantCulture, $"""
            <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" style="background:{PageBg};padding:32px 16px;">
              <tr><td align="center">
                <table role="presentation" width="560" cellpadding="0" cellspacing="0" border="0" style="max-width:560px;width:100%;background:{CardBg};border:1px solid {Border};border-radius:14px;overflow:hidden;">
            """);

        // ---- Header strip --------------------------------------------------
        html.Append(CultureInfo.InvariantCulture, $"""
            <tr><td style="padding:18px 28px;border-bottom:1px solid {Border};">
              <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0">
                <tr>
                  <td style="font-size:14px;font-weight:600;letter-spacing:-0.01em;color:{Ink};">{HtmlEncode(sender)}</td>
                  <td align="right" style="font-size:11px;text-transform:uppercase;letter-spacing:0.06em;color:{Muted};">DueMap</td>
                </tr>
              </table>
            </td></tr>
            """);

        // ---- Headline + body content ---------------------------------------
        html.Append(CultureInfo.InvariantCulture, $"""
            <tr><td style="padding:28px 28px 8px 28px;">
              <h1 style="margin:0 0 14px 0;font-size:22px;line-height:1.25;font-weight:600;letter-spacing:-0.01em;color:{Ink};">
                {HtmlEncode(req.Headline)}
              </h1>
              <p style="margin:0 0 14px 0;font-size:15px;line-height:1.55;color:{Ink};">
                {req.IntroParagraph}
              </p>
            """);

        foreach (var p in req.AdditionalParagraphs)
        {
            html.Append(CultureInfo.InvariantCulture, $"""
              <p style="margin:0 0 14px 0;font-size:15px;line-height:1.55;color:{Ink};">{p}</p>
            """);
        }

        // ---- Primary CTA button (optional) ---------------------------------
        if (!string.IsNullOrWhiteSpace(req.PrimaryCtaUrl) && !string.IsNullOrWhiteSpace(req.PrimaryCtaLabel))
        {
            html.Append(CultureInfo.InvariantCulture, $"""
              <table role="presentation" cellpadding="0" cellspacing="0" border="0" style="margin:22px 0 8px 0;">
                <tr><td bgcolor="{Accent}" style="border-radius:10px;">
                  <a href="{HtmlEncode(req.PrimaryCtaUrl)}"
                     style="display:inline-block;padding:12px 22px;font-size:14px;font-weight:600;color:#ffffff;text-decoration:none;border-radius:10px;background:{Accent};border:1px solid {AccentDark};">
                    {HtmlEncode(req.PrimaryCtaLabel)}
                  </a>
                </td></tr>
              </table>
            """);
        }

        // ---- Optional muted footer note inside the card --------------------
        if (!string.IsNullOrWhiteSpace(req.FooterNote))
        {
            html.Append(CultureInfo.InvariantCulture, $"""
              <p style="margin:18px 0 0 0;font-size:13px;line-height:1.55;color:{Muted};">{req.FooterNote}</p>
            """);
        }

        html.Append("</td></tr>");

        // ---- Bottom strip --------------------------------------------------
        html.Append(CultureInfo.InvariantCulture, $"""
            <tr><td style="padding:18px 28px;border-top:1px solid {Border};background:{PageBg};">
              <p style="margin:0;font-size:11px;line-height:1.5;color:{Muted};">
                Sent by DueMap{(string.IsNullOrWhiteSpace(req.WorkspaceName) ? "" : $" on behalf of {HtmlEncode(req.WorkspaceName!)}")}.
                If you weren't expecting this email, you can safely ignore it.
              </p>
            </td></tr>
            """);

        // ---- Close ---------------------------------------------------------
        html.Append("""
                </table>
              </td></tr>
            </table>
            </body></html>
            """);

        var text = BuildPlainText(req);
        return new BrandedEmail(req.Subject, html.ToString(), text);
    }

    private static string BuildPlainText(TransactionalEmailRequest req)
    {
        var sb = new StringBuilder(1024);
        sb.AppendLine(req.Headline);
        sb.AppendLine(new string('=', Math.Min(req.Headline.Length, 60)));
        sb.AppendLine();
        sb.AppendLine(StripHtml(req.IntroParagraph));

        foreach (var p in req.AdditionalParagraphs)
        {
            sb.AppendLine();
            sb.AppendLine(StripHtml(p));
        }

        if (!string.IsNullOrWhiteSpace(req.PrimaryCtaUrl) && !string.IsNullOrWhiteSpace(req.PrimaryCtaLabel))
        {
            sb.AppendLine();
            sb.AppendLine(CultureInfo.InvariantCulture, $"{req.PrimaryCtaLabel}: {req.PrimaryCtaUrl}");
        }

        if (!string.IsNullOrWhiteSpace(req.FooterNote))
        {
            sb.AppendLine();
            sb.AppendLine(StripHtml(req.FooterNote));
        }

        sb.AppendLine();
        sb.AppendLine("—");
        sb.Append("Sent by DueMap");
        if (!string.IsNullOrWhiteSpace(req.WorkspaceName))
        {
            sb.Append(CultureInfo.InvariantCulture, $" on behalf of {req.WorkspaceName}");
        }
        sb.AppendLine(".");
        sb.AppendLine("If you weren't expecting this email, you can safely ignore it.");

        return sb.ToString();
    }

    // Minimal HTML-tag stripper for the plain-text fallback. We trust callers
    // to supply already-sanitised content; this is purely about converting
    // <strong>/<a> wrappers in the HTML body back to flat text for the alt.
    private static string StripHtml(string? raw)
    {
        if (string.IsNullOrEmpty(raw)) return string.Empty;
        var sb = new StringBuilder(raw.Length);
        var inTag = false;
        foreach (var ch in raw)
        {
            if (ch == '<') { inTag = true; continue; }
            if (ch == '>') { inTag = false; continue; }
            if (!inTag) sb.Append(ch);
        }
        return WebUtility.HtmlDecode(sb.ToString()).Trim();
    }

    private static string HtmlEncode(string raw) => WebUtility.HtmlEncode(raw);
}

/// <summary>
/// Caller-supplied content. Paragraph fields may contain inline HTML
/// (<c>&lt;strong&gt;</c>, <c>&lt;a href&gt;</c>, etc.) and are emitted
/// verbatim — sanitise upstream if the content comes from user input.
/// </summary>
public sealed record TransactionalEmailRequest(
    string   Subject,
    string   Headline,
    string   IntroParagraph,
    string[] AdditionalParagraphs,
    string?  PrimaryCtaLabel = null,
    string?  PrimaryCtaUrl = null,
    string?  FooterNote = null,
    string?  WorkspaceName = null,
    string?  Preheader = null);

public sealed record BrandedEmail(string Subject, string BodyHtml, string BodyText);
