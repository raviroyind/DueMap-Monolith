using DueMap.Integrations.Notices;

namespace DueMap.Web.Services;

/// <summary>
/// Shared builder/sender for account-lifecycle emails (email confirmation and
/// password reset). Centralised so the pages that trigger the same message —
/// Register and the "resend" action, for instance — can't drift apart.
/// </summary>
public static class AccountEmails
{
    // Hoisted so the constant array isn't re-allocated per call (CA1861).
    private static readonly string[] ConfirmExtraParagraphs =
    {
        "This link expires in 24 hours. Until you confirm, your account stays inactive and you won't be able to sign in."
    };

    private static readonly string[] ResetExtraParagraphs =
    {
        "This link expires in one hour and can only be used once. Your current password keeps working until you choose a new one."
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

    /// <summary>
    /// Builds and sends the "reset your password" message.
    ///
    /// Same best-effort contract as the confirmation mail: the caller shows the
    /// identical generic response whether or not the address exists, so a send
    /// failure must not change what the visitor sees. The footer line matters —
    /// it tells someone who didn't request this that no action is needed, which
    /// is the only signal they get that their account wasn't touched.
    /// </summary>
    public static Task<DispatchResult> SendPasswordResetAsync(
        IEmailSender email, string toEmail, string displayName, string resetUrl, CancellationToken ct)
    {
        var branded = TransactionalEmailBuilder.Build(new TransactionalEmailRequest(
            Subject: "Reset your DueMap password",
            Headline: "Reset your password",
            IntroParagraph:
                "We received a request to reset the password for your DueMap account. " +
                "Choose a new one using the link below.",
            AdditionalParagraphs: ResetExtraParagraphs,
            PrimaryCtaLabel: "Choose a new password →",
            PrimaryCtaUrl: resetUrl,
            FooterNote:
                "If you didn't request a password reset, you can safely ignore this email — " +
                "your password hasn't changed.",
            WorkspaceName: null,                    // from DueMap itself, not a PM
            Preheader: "Use this link to choose a new DueMap password."));

        return email.SendAsync(new DispatchRequest(
            Channel: DispatchChannel.Email,
            To: toEmail,
            ToDisplayName: displayName,
            Subject: branded.Subject,
            BodyHtml: branded.BodyHtml,
            BodyText: branded.BodyText), ct);
    }
}
