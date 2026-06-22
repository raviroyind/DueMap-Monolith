using DueMap.Billing.Domain;
using DueMap.Billing.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace DueMap.Billing.Services;

internal sealed class AssessmentRunRepository : IAssessmentRunRepository
{
    // SQL Server error number for unique-constraint / unique-index violations.
    private const int SqlUniqueViolation = 2627;
    private const int SqlIndexViolation  = 2601;

    private readonly BillingDbContext _db;

    public AssessmentRunRepository(BillingDbContext db)
    {
        _db = db;
    }

    public Task<bool> HasRunAsync(int leaseId, DateOnly dueDate, ActionKind kind, CancellationToken ct, string? stepKey = null) =>
        _db.AssessmentRuns.AsNoTracking()
            .AnyAsync(r => r.LeaseId == leaseId && r.DueDate == dueDate && r.ActionKind == kind && r.StepKey == stepKey, ct);

    public async Task<AssessmentRun?> TryRecordAsync(AssessmentRun run, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(run);

        if (run.Id != 0)
        {
            throw new InvalidOperationException("AssessmentRun.Id must be 0 on insert.");
        }

        if (run.CreatedAt == default)
        {
            run.CreatedAt = DateTime.UtcNow;
        }

        _db.AssessmentRuns.Add(run);
        try
        {
            await _db.SaveChangesAsync(ct);
            return run;
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // Another process / earlier retry already recorded this action.
            // Detach the failed entity so the caller can reuse the context.
            _db.Entry(run).State = EntityState.Detached;
            return null;
        }
    }

    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is SqlException sql
        && (sql.Number == SqlUniqueViolation || sql.Number == SqlIndexViolation);
}
