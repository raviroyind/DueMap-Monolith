using DueMap.Tenancy.Domain;
using DueMap.Tenancy.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DueMap.Tenancy.Services;

internal sealed class LeaseWriter : ILeaseWriter
{
    private readonly IDbContextFactory<TenancyDbContext> _dbFactory;

    public LeaseWriter(IDbContextFactory<TenancyDbContext> dbFactory)
        => _dbFactory = dbFactory;

    public async Task<Lease> CreateAsync(NewLeaseInput input, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (input.MonthlyRent <= 0m)
        {
            throw new ArgumentException("Monthly rent must be positive.", nameof(input));
        }
        if (input.EndDate is DateOnly end && end < input.StartDate)
        {
            throw new ArgumentException("End date cannot precede start date.", nameof(input));
        }

        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        // Customer ownership check — prevents linking a lease to a customer in
        // another PM's workspace (which would silently break tenant scoping).
        if (input.CustomerId is int cid)
        {
            var customerPm = await db.Customers.AsNoTracking()
                .Where(c => c.Id == cid)
                .Select(c => (int?)c.PropertyManagerId)
                .FirstOrDefaultAsync(ct);
            if (customerPm is null)
            {
                throw new InvalidOperationException($"Customer {cid} not found.");
            }
            if (customerPm != input.PropertyManagerId)
            {
                throw new InvalidOperationException(
                    $"Customer {cid} belongs to PM {customerPm}, not the target PM {input.PropertyManagerId}.");
            }
        }

        var lease = new Lease
        {
            PropertyManagerId = input.PropertyManagerId,
            StateId = input.StateId,
            CustomerId = input.CustomerId,
            MonthlyRent = input.MonthlyRent,
            StartDate = input.StartDate,
            EndDate = input.EndDate,
            UnitLabel = string.IsNullOrWhiteSpace(input.UnitLabel) ? null : input.UnitLabel.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        db.Leases.Add(lease);
        await db.SaveChangesAsync(ct);
        return lease;
    }

    public async Task UpdateAutopayStatusAsync(int leaseId, AutopayStatus status, DateTime checkedAt, CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var lease = await db.Leases.FirstOrDefaultAsync(l => l.Id == leaseId, ct);
        if (lease is null) return;   // lease vanished between read + write; nothing to stamp

        lease.AutopayStatus = status;
        lease.AutopayCheckedAt = checkedAt;
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateCoreFieldsAsync(int leaseId, decimal monthlyRent, int stateId, CancellationToken ct)
    {
        if (monthlyRent <= 0m)
        {
            throw new ArgumentException("Monthly rent must be positive.", nameof(monthlyRent));
        }

        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var lease = await db.Leases.FirstOrDefaultAsync(l => l.Id == leaseId, ct)
            ?? throw new InvalidOperationException($"Lease {leaseId} not found.");

        lease.MonthlyRent = monthlyRent;
        lease.StateId = stateId;
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateLateFeeProfileAsync(int leaseId, LateFeeProfileInput profile, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(profile);

        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var lease = await db.Leases.FirstOrDefaultAsync(l => l.Id == leaseId, ct)
            ?? throw new InvalidOperationException($"Lease {leaseId} not found.");

        lease.LateFeeType         = profile.LateFeeType;
        lease.LateFeePercent      = profile.LateFeePercent;
        lease.LateFeeFlatAmount   = profile.LateFeeFlatAmount;
        lease.LateFeeGraceDays    = profile.GraceDays;
        lease.LateFeeDailyAccrual = profile.DailyAccrual;
        // Staged on every AutoSetup write — the explicit "Go live" action
        // (P1-4) flips this back to false. Re-running AutoSetup re-stages.
        lease.FeesStaged = true;

        await db.SaveChangesAsync(ct);
    }
}
