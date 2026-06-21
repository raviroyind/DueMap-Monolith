namespace DueMap.Tenancy.Domain;

/// <summary>
/// Whether a lease's tenant is on autopay (P2-1). Because no accounting API
/// exposes a true enrollment flag, this is an inferred proxy from payment
/// behavior (plus, later, an optional manual PM override). Autopay-aware
/// planning (P2-4) suppresses payment nags for <see cref="Enrolled"/>.
/// </summary>
public enum AutopayStatus : byte
{
    Unknown  = 0,   // not enough history to tell
    None     = 1,   // not on autopay / not a reliable on-time payer
    Enrolled = 2    // on autopay or reliably pays on time
}
