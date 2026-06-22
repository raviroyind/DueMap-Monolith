using DueMap.Common.FeatureFlags;
using DueMap.Integrations.Accounting;
using DueMap.Integrations.Services;
using DueMap.Tenancy.Domain;
using NSubstitute;
using Xunit;

namespace DueMap.Integrations.Tests;

/// <summary>
/// P2-1: autopay deep-link building (per provider, flag-gated) + status
/// inference. Money-untouched — the "deep link" is just the provider's hosted
/// pay page, and status is a behavioral proxy.
/// </summary>
public sealed class AutopayServiceTests
{
    private readonly IFeatureFlags _flags = Substitute.For<IFeatureFlags>();

    private AutopayService NewSut(bool flagOn)
    {
        _flags.IsEnabledAsync("integrations.autopay", null, Arg.Any<CancellationToken>()).Returns(flagOn);
        return new AutopayService(_flags);
    }

    [Theory]
    [InlineData(AccountingProvider.QuickBooks)]
    [InlineData(AccountingProvider.Xero)]
    public async Task Build_url_returns_pay_url_per_provider_when_enabled(AccountingProvider provider)
    {
        var url = "https://pay.example.com/inv/123";
        var result = await NewSut(flagOn: true).BuildSetupUrlAsync(provider, url, CancellationToken.None);
        Assert.Equal(url, result);
    }

    [Fact]
    public async Task Build_url_returns_null_when_flag_off()
    {
        var result = await NewSut(flagOn: false)
            .BuildSetupUrlAsync(AccountingProvider.QuickBooks, "https://pay.example.com/inv/123", CancellationToken.None);
        Assert.Null(result);
    }

    [Fact]
    public async Task Build_url_returns_null_when_no_pay_url()
    {
        var result = await NewSut(flagOn: true)
            .BuildSetupUrlAsync(AccountingProvider.Xero, null, CancellationToken.None);
        Assert.Null(result);
    }

    // ---- status inference (pure) ----

    [Fact]
    public void Consistently_paid_history_infers_enrolled()
    {
        Assert.Equal(AutopayStatus.Enrolled,
            AutopayStatusInference.Infer(new[] { true, true, true, true }));
    }

    [Fact]
    public void Mostly_unpaid_history_infers_none()
    {
        Assert.Equal(AutopayStatus.None,
            AutopayStatusInference.Infer(new[] { true, false, false }));
    }

    [Fact]
    public void Sparse_history_is_unknown()
    {
        Assert.Equal(AutopayStatus.Unknown, AutopayStatusInference.Infer(new[] { true }));
        Assert.Equal(AutopayStatus.Unknown, AutopayStatusInference.Infer(System.Array.Empty<bool>()));
    }

    [Fact]
    public void Eighty_percent_paid_is_the_enrolled_threshold()
    {
        // 4/5 = 0.8 → enrolled; 3/5 = 0.6 → none.
        Assert.Equal(AutopayStatus.Enrolled, AutopayStatusInference.Infer(new[] { true, true, true, true, false }));
        Assert.Equal(AutopayStatus.None,     AutopayStatusInference.Infer(new[] { true, true, true, false, false }));
    }
}
