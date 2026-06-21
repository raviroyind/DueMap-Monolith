using DueMap.Common.FeatureFlags;
using DueMap.Integrations.Notices;
using DueMap.Integrations.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Twilio.Clients;
using Xunit;

namespace DueMap.Integrations.Tests;

/// <summary>
/// P2-2 acceptance: the SMS sender refuses to send when the channel is off OR
/// the recipient opted out, returning <see cref="DispatchStatus.Suppressed"/>
/// (not Failed) — without ever touching Twilio. The actual happy-path send hits
/// Twilio's static client and is covered at the integration level, not here.
/// </summary>
public sealed class TwilioSmsSenderGateTests
{
    private readonly ITwilioRestClient _client = Substitute.For<ITwilioRestClient>();
    private readonly IFeatureFlags _flags = Substitute.For<IFeatureFlags>();
    private readonly ISmsSuppressionStore _suppressions = Substitute.For<ISmsSuppressionStore>();

    private TwilioSmsSender NewSut() => new(
        _client,
        Options.Create(new IntegrationsOptions
        {
            Twilio = new IntegrationsOptions.TwilioOptions { FromNumber = "+15005550006" }
        }),
        _flags, _suppressions, NullLogger<TwilioSmsSender>.Instance);

    private static DispatchRequest SmsRequest() => new(
        Channel: DispatchChannel.Sms, To: "+14155551234", ToDisplayName: "Jane",
        Subject: "", BodyHtml: "", BodyText: "Your rent is due.");

    [Fact]
    public async Task Flag_off_suppresses_without_checking_opt_out_or_sending()
    {
        _flags.IsEnabledAsync("notices.sms", null, Arg.Any<CancellationToken>()).Returns(false);

        var result = await NewSut().SendAsync(SmsRequest(), CancellationToken.None);

        Assert.Equal(DispatchStatus.Suppressed, result.Status);
        Assert.Contains("disabled", result.FailureReason, StringComparison.OrdinalIgnoreCase);
        // Channel off short-circuits before the opt-out lookup.
        await _suppressions.DidNotReceive().IsSuppressedAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Opted_out_recipient_is_suppressed()
    {
        _flags.IsEnabledAsync("notices.sms", null, Arg.Any<CancellationToken>()).Returns(true);
        _suppressions.IsSuppressedAsync("+14155551234", Arg.Any<CancellationToken>()).Returns(true);

        var result = await NewSut().SendAsync(SmsRequest(), CancellationToken.None);

        Assert.Equal(DispatchStatus.Suppressed, result.Status);
        Assert.Contains("opted out", result.FailureReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Flag_on_but_no_from_number_is_failed_not_suppressed()
    {
        _flags.IsEnabledAsync("notices.sms", null, Arg.Any<CancellationToken>()).Returns(true);

        var sut = new TwilioSmsSender(
            _client,
            Options.Create(new IntegrationsOptions { Twilio = new IntegrationsOptions.TwilioOptions { FromNumber = null } }),
            _flags, _suppressions, NullLogger<TwilioSmsSender>.Instance);

        var result = await sut.SendAsync(SmsRequest(), CancellationToken.None);

        Assert.Equal(DispatchStatus.Failed, result.Status);
    }
}
