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
}
