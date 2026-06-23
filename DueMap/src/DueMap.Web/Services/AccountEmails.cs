using DueMap.Integrations.Notices;

namespace DueMap.Web.Services;

/// <summary>
/// Shared builder/sender for account-lifecycle emails (currently just the
/// email-confirmation link). Centralised so the Register page and the
/// "resend" action on RegisterConfirmation can't drift apart.
/// </summary>
public static class AccountEmails
{
    // Hoisted so the constant array isn't re-allocated per call (CA1861).
    private static readonly string[] ConfirmExtraParagraphs =
    {
        "This link expires in 24 hours. Until you confirm, your account stays inactive and you won't be able to sign in."
    };

    /// <summary>
    /// Builds and sends the "confirm your email to activate DueMap" message.
    /// Best-effort by contract: callers wrap this in try/catch and route the
    /// user to the check-your-email page regardless, so a provider hiccup never
    /// strands a new sign-up.
    /// </summary>
    public static Task<DispatchResult> SendConfirmationAsync(
        IEmailSender email, string toEmail, string displayName, string confirmUrl, CancellationToken ct)
    {
        var branded = TransactionalEmailBuilder.Build(new TransactionalEmailRequest(
            Subject: "Confirm your email to activate DueMap",
            Headline: "Confirm your email",
            IntroParagraph:
                "Thanks for signing up for DueMap. Confirm this email address to activate your " +
                "account — then we'll help you connect QuickBooks or Xero and set up automated " +
                "rent reminders and late fees.",
            AdditionalParagraphs: ConfirmExtraParagraphs,
            PrimaryCtaLabel: "Confirm my email →",
            PrimaryCtaUrl: confirmUrl,
            FooterNote: "If you didn't create a DueMap account, you can safely ignore this email.",
            WorkspaceName: null,                    // this email IS from DueMap, not a PM
            Preheader: "Confirm your email address to activate your DueMap account."));

        return email.SendAsync(new DispatchRequest(
            Channel: DispatchChannel.Email,
            To: toEmail,
            ToDisplayName: displayName,
            Subject: branded.Subject,
            BodyHtml: branded.BodyHtml,
            BodyText: branded.BodyText), ct);
    }
}
