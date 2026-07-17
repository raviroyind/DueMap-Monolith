namespace DueMap.Integrations.Accounting;

/// <summary>
/// One row per PM — at most one active accounting connection. Tokens are
/// stored encrypted (ASP.NET Core Data Protection); never read directly,
/// always through the connection service which decrypts via
/// <see cref="ITokenProtector"/>.
/// </summary>
public sealed class PmAccountingConnection
{
    public int PropertyManagerId { get; set; }
    public AccountingProvider Provider { get; set; }
    public string RealmId { get; set; } = default!;

    /// <summary>
    /// The QBO/Xero organisation name ("Sandbox Company_US_1"), captured at
    /// connect time and backfilled by sync when missing. The human-facing
    /// identity everywhere the raw realm id used to leak.
    /// </summary>
    public string? CompanyName { get; set; }

    public string AccessTokenProtected { get; set; } = default!;
    public string RefreshTokenProtected { get; set; } = default!;
    public DateTime AccessTokenExpiresAt { get; set; }
    public DateTime? RefreshTokenExpiresAt { get; set; }
    public string? Scopes { get; set; }

    public DateTime ConnectedAt { get; set; }
    public DateTime? LastSyncAt { get; set; }
    public string? LastSyncError { get; set; }
    public ConnectionStatus Status { get; set; } = ConnectionStatus.Connected;
    public DateTime UpdatedAt { get; set; }

    // ---- v17: operational health ---------------------------------------
    /// <summary>Operational liveness — orthogonal to <see cref="Status"/>. See enum docs.</summary>
    public ConnectionHealthStatus HealthStatus { get; set; } = ConnectionHealthStatus.Healthy;

    /// <summary>When health was last set. Bumped on every Mark{Broken,Healthy}Async call.</summary>
    public DateTime? LastHealthCheck { get; set; }

    /// <summary>Short human reason for the current <see cref="ConnectionHealthStatus.Broken"/> state.</summary>
    public string? PausedReason { get; set; }

    // ---- v28: disconnect telemetry --------------------------------------
    /// <summary>Optional PM-supplied reason from the disconnect flow. Cleared on reconnect.</summary>
    public string? DisconnectReason { get; set; }
    public DateTime? DisconnectedAt { get; set; }
}

/// <summary>
/// Thrown when a connect/reconnect attempt targets a DIFFERENT accounting
/// company (realm/tenant, or a different provider) than the one this
/// workspace is bound to. One workspace = one set of books: hydrating a new
/// realm over existing customers/invoices/leases would silently mix two
/// portfolios. The UI turns this into a "this workspace is linked to X"
/// explanation instead of letting the switch happen.
/// </summary>
public sealed class RealmMismatchException : InvalidOperationException
{
    public AccountingProvider CurrentProvider { get; }
    public string CurrentRealmId { get; }
    public AccountingProvider AttemptedProvider { get; }
    public string AttemptedRealmId { get; }

    public RealmMismatchException(
        AccountingProvider currentProvider, string currentRealmId,
        AccountingProvider attemptedProvider, string attemptedRealmId)
        : base($"This workspace is bound to {currentProvider} company (realm {currentRealmId}); " +
               $"refusing to connect {attemptedProvider} realm {attemptedRealmId}.")
    {
        CurrentProvider = currentProvider;
        CurrentRealmId = currentRealmId;
        AttemptedProvider = attemptedProvider;
        AttemptedRealmId = attemptedRealmId;
    }
}
