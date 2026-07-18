using DueMap.Tenancy;

namespace DueMap.Web.Services;

/// <summary>
/// Per-circuit resolution of the workspace's time zone — the zone captured
/// from the PM's QuickBooks/Xero organisation at connect time (or chosen in
/// Daily Close settings). Every PM-facing timestamp renders through this so
/// the app speaks the PM's local time instead of raw UTC. Same shape as
/// <see cref="PmMoney"/>: resolve once, cache for the circuit, fail safe.
/// </summary>
public sealed class PmClock
{
    private readonly PmContext _pm;
    private readonly IPropertyManagerReader _pmReader;
    private TimeZoneInfo? _cached;

    public PmClock(PmContext pm, IPropertyManagerReader pmReader)
    {
        _pm = pm;
        _pmReader = pmReader;
    }

    public async Task<TimeZoneInfo> GetZoneAsync(CancellationToken ct = default)
    {
        if (_cached is not null) return _cached;
        try
        {
            var pmId = await _pm.GetPmIdAsync();
            var pm = await _pmReader.GetAsync(pmId, ct);
            _cached = Resolve(pm?.TimeZoneId);
        }
        catch
        {
            _cached = TimeZoneInfo.Utc;
        }
        return _cached;
    }

    private static TimeZoneInfo Resolve(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) return TimeZoneInfo.Utc;
        try
        {
            // .NET 6+ accepts IANA ids on Windows via ICU, so the same stored
            // id works on the dev box and in Linux containers.
            return TimeZoneInfo.FindSystemTimeZoneById(id);
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return TimeZoneInfo.Utc;
        }
    }
}
