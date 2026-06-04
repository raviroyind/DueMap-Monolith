using DueMap.Tenancy.Domain;
using DueMap.Tenancy.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DueMap.Tenancy.Services;

/// <summary>
/// Uses <see cref="IDbContextFactory{TenancyDbContext}"/> rather than a scoped
/// context — see TenancyModule for the full rationale. Each method creates +
/// disposes its own short-lived DbContext so concurrent Blazor navigations
/// (MainLayout's gate check + a step page's load) never race on a shared one.
/// </summary>
internal sealed class OnboardingProgressService : IOnboardingProgressService
{
    private readonly IDbContextFactory<TenancyDbContext> _dbFactory;

    public OnboardingProgressService(IDbContextFactory<TenancyDbContext> dbFactory)
        => _dbFactory = dbFactory;

    public async Task<OnboardingProgress> GetAsync(int pmId, CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var row = await db.PropertyManagers
            .AsNoTracking()
            .Where(p => p.Id == pmId)
            .Select(p => new
            {
                p.StepConnectDoneAt,
                p.StepCloseDoneAt,
                p.StepNoticePrefsDoneAt,
                p.StepPreflightDoneAt
            })
            .FirstOrDefaultAsync(ct);

        if (row is null)
            return new OnboardingProgress(null, null, null, null);

        return new OnboardingProgress(
            row.StepConnectDoneAt,
            row.StepCloseDoneAt,
            row.StepNoticePrefsDoneAt,
            row.StepPreflightDoneAt);
    }

    public Task MarkConnectDoneAsync(int pmId, CancellationToken ct) =>
        StampAsync(pmId, p => p.StepConnectDoneAt, ct);

    public Task MarkCloseDoneAsync(int pmId, CancellationToken ct) =>
        StampAsync(pmId, p => p.StepCloseDoneAt, ct);

    public Task MarkNoticePrefsDoneAsync(int pmId, CancellationToken ct) =>
        StampAsync(pmId, p => p.StepNoticePrefsDoneAt, ct);

    public async Task MarkPreflightDoneAsync(int pmId, CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var pm = await db.PropertyManagers.FirstOrDefaultAsync(p => p.Id == pmId, ct)
            ?? throw new InvalidOperationException($"PM {pmId} not found.");

        // Idempotent: only stamp the first time so the "completed at" reflects
        // when onboarding actually finished, not the last edit.
        pm.StepPreflightDoneAt ??= DateTime.UtcNow;

        // Going live: status only advances.
        if ((int)pm.OnboardingStatus < (int)OnboardingStatus.Active)
            pm.OnboardingStatus = OnboardingStatus.Active;

        await db.SaveChangesAsync(ct);
    }

    private async Task StampAsync(
        int pmId,
        System.Linq.Expressions.Expression<Func<PropertyManager, DateTime?>> selector,
        CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var pm = await db.PropertyManagers.FirstOrDefaultAsync(p => p.Id == pmId, ct)
            ?? throw new InvalidOperationException($"PM {pmId} not found.");

        // Expression → PropertyInfo so we can SET the value.
        var member = (System.Linq.Expressions.MemberExpression)selector.Body;
        var prop   = (System.Reflection.PropertyInfo)member.Member;

        if (prop.GetValue(pm) is null)
        {
            prop.SetValue(pm, DateTime.UtcNow);
            await db.SaveChangesAsync(ct);
        }
    }
}
