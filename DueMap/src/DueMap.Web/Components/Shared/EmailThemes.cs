// "Shared" is a reserved word in VB but we don't ship a VB consumer; the folder
// name is the standard Blazor convention for components shared across pages.
#pragma warning disable CA1716
namespace DueMap.Web.Components.Shared;
#pragma warning restore CA1716

/// <summary>
/// Static catalogue of email "skins" the PM can pick from on the template
/// editor. Each theme is a self-contained chrome (header strip + body card +
/// footer) that wraps the PM's HTML content. All styles are inline because
/// email clients strip <style> blocks and external stylesheets.
///
/// <para>v1 scope: <b>preview-only</b>. The picker shows the PM what the
/// branded email would look like, but the actual send pipeline still emits
/// the raw body HTML. Wiring the chrome to the renderer requires a schema
/// migration (theme_key column on pm_template_overrides) plus a hook in the
/// notice render pipeline — tracked as a separate task.</para>
/// </summary>
public static class EmailThemes
{
    public sealed record Theme(
        string Key,
        string Label,
        string AccentHex,
        string HeadlineHex,
        string HeadlineCopy);

    /// <summary>Default theme — used when nothing is selected.</summary>
    public const string DefaultKey = "indigo";

    public static readonly IReadOnlyList<Theme> All =
    [
        new("indigo",  "Standard", "#4F46E5", "#FFFFFF", "A note about your rent"),
        new("slate",   "Neutral",  "#475569", "#FFFFFF", "A note about your rent"),
        new("emerald", "Friendly", "#059669", "#FFFFFF", "Rent reminder"),
        new("amber",   "Warning",  "#D97706", "#FFFFFF", "Action needed"),
        new("rose",    "Urgent",   "#E11D48", "#FFFFFF", "Important: rent payment")
    ];

    public static Theme Resolve(string? key) =>
        All.FirstOrDefault(t => t.Key == key) ?? All[0];

    /// <summary>
    /// Wraps the supplied inner body HTML in the theme's chrome. Inline CSS
    /// only — keep it boring so every mail client renders consistently.
    /// </summary>
    public static string Wrap(string innerBodyHtml, Theme theme) => $$"""
        <div style="max-width:600px;margin:0 auto;font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',sans-serif;background:#f4f4f5;padding:24px 0;">
          <div style="background:{{theme.AccentHex}};padding:22px 28px;border-radius:12px 12px 0 0;color:{{theme.HeadlineHex}};">
            <div style="font-size:11px;font-weight:600;letter-spacing:0.12em;text-transform:uppercase;opacity:0.82;">DueMap</div>
            <div style="font-size:19px;font-weight:600;margin-top:6px;line-height:1.3;">{{theme.HeadlineCopy}}</div>
          </div>
          <div style="background:#ffffff;padding:26px 28px;color:#27272a;line-height:1.65;font-size:15px;border-radius:0 0 12px 12px;">
            {{innerBodyHtml}}
          </div>
          <div style="padding:18px 28px 4px;text-align:center;font-size:11px;color:#a1a1aa;">
            Sent by DueMap on behalf of your property manager.
          </div>
        </div>
        """;
}
