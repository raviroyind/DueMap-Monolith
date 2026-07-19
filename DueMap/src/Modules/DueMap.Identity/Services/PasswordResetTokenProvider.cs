using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DueMap.Identity.Services;

/// <summary>
/// Options for <see cref="PasswordResetTokenProvider{TUser}"/>.
///
/// Exists purely so password-reset tokens can carry their own lifespan.
/// Identity's built-in providers all share one
/// <see cref="DataProtectionTokenProviderOptions"/> instance, so shortening the
/// reset window there would also shorten email confirmation — which our
/// confirmation email explicitly promises lasts 24 hours.
/// </summary>
public sealed class PasswordResetTokenProviderOptions : DataProtectionTokenProviderOptions
{
    public PasswordResetTokenProviderOptions()
    {
        Name = "DueMapPasswordReset";

        // A reset link is a live credential sitting in an inbox: anyone who can
        // read the mailbox can take the account for as long as it stays valid.
        // One hour is long enough to walk away from the keyboard and come back,
        // short enough that an old message in a breached mailbox is inert.
        TokenLifespan = TimeSpan.FromHours(1);
    }
}

/// <summary>
/// Password-reset token provider with a one-hour lifespan, registered under the
/// name Identity looks up via <c>IdentityOptions.Tokens.PasswordResetTokenProvider</c>.
/// Behaviour is otherwise the stock data-protection provider.
/// </summary>
public sealed class PasswordResetTokenProvider<TUser> : DataProtectorTokenProvider<TUser>
    where TUser : class
{
    public const string ProviderName = "DueMapPasswordReset";

    public PasswordResetTokenProvider(
        IDataProtectionProvider dataProtectionProvider,
        IOptions<PasswordResetTokenProviderOptions> options,
        ILogger<DataProtectorTokenProvider<TUser>> logger)
        : base(dataProtectionProvider, options, logger)
    {
    }
}
