namespace DueMap.Integrations.Accounting;

public sealed record ConnectInitiationResult(string AuthorizationUrl, string State);

public sealed record ConnectionCallbackInput(
    AccountingProvider Provider,
    string Code,
    string State,
    string? RealmId);   // QB supplies it; Xero discovers it later

/// <summary>
/// The public surface for the PM-onboarding OAuth flow.
/// <list type="number">
///   <item><see cref="InitiateAsync"/> creates an oauth_attempts row, returns the authorize URL.</item>
///   <item>Browser bounces to provider, returns to <c>/oauth/{provider}/callback</c>.</item>
///   <item><see cref="CompleteAsync"/> verifies state, exchanges code, encrypts + stores tokens.</item>
/// </list>
/// </summary>
public interface IAccountingConnectionService
{
    Task<ConnectInitiationResult> InitiateAsync(
        int propertyManagerId,
        AccountingProvider provider,
        string redirectUri,
        CancellationToken ct);

    Task<PmAccountingConnection> CompleteAsync(
        ConnectionCallbackInput input,
        CancellationToken ct);

    Task<PmAccountingConnection?> GetActiveAsync(int propertyManagerId, CancellationToken ct);

    /// <summary>
    /// Decrypted access token for outbound API calls. Auto-refreshes if expired.
    /// </summary>
    Task<string?> GetAccessTokenAsync(int propertyManagerId, CancellationToken ct);

    Task DisconnectAsync(int propertyManagerId, CancellationToken ct);

    /// <summary>
    /// Persists an already-exchanged OAuth token bundle as the PM's accounting
    /// connection — no state validation, no oauth_attempts dance. Used by the
    /// Sign-In-with-Intuit-via-QBO-OAuth flow where the connection is
    /// established as a side effect of identity sign-in, so the usual
    /// initiate/complete pairing doesn't apply.
    /// </summary>
    Task<PmAccountingConnection> PersistDirectAsync(
        int propertyManagerId,
        AccountingProvider provider,
        DueMap.Integrations.Accounting.OAuth.OAuthTokens tokens,
        CancellationToken ct);
}
