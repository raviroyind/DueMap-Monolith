namespace DueMap.Common.Ops;

/// <summary>
/// Operator-alert severity. Maps to subject prefixes ([INFO], [WARN], [CRIT])
/// so an inbox filter can route by importance without parsing the body.
/// </summary>
public enum AlertSeverity
{
    Info     = 0,
    Warning  = 1,
    Critical = 2
}
