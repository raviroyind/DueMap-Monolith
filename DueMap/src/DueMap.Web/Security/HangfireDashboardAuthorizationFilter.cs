using Hangfire.Dashboard;

namespace DueMap.Web.Security;

/// <summary>
/// Gates the Hangfire dashboard at <c>/hangfire</c>. Any authenticated user
/// can see the dashboard for now — there's only one role implicit in v1 (the
/// PM admin who registered). When a real "PlatformAdmin" role lands, switch
/// this to <c>context.GetHttpContext().User.IsInRole("PlatformAdmin")</c>.
///
/// Hangfire runs this filter directly inside its dashboard middleware. The
/// ASP.NET fallback authorize policy doesn't apply to Hangfire's pipeline,
/// which is why we need this explicit gate — without it, <c>/hangfire</c>
/// would be reachable by anyone on the internet.
/// </summary>
internal sealed class HangfireDashboardAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var http = context.GetHttpContext();
        return http.User.Identity?.IsAuthenticated == true;
    }
}
