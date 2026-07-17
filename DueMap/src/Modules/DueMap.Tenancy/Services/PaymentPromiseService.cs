using DueMap.Tenancy.Domain;
using DueMap.Tenancy.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DueMap.Tenancy.Services;

internal sealed class PaymentPromiseService : IPaymentPromiseService
{
    private readonly IDbContextFactory<TenancyDbContext> _dbFactory;

    public PaymentPromiseService(IDbContextFactory<TenancyDbContext> dbFactory)
        => _dbFactory = dbFactory;

    public async Task<PaymentPromise> CreateAsync(NewPromiseInput input, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (input.Amount is <= 0m)
        {
            throw new ArgumentException("Promised amount must be greater than zero (or blank for the full balance).", nameof(input));
        }
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (input.PromisedDate < today)
        {
            throw new ArgumentException("Promised date can't be in the past.", nameof(input));
        }

        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        // Ownership check — a promise on another PM's lease would silently
        // pause someone else's collections.
        var leasePm = await db.Leases.AsNoTracking()
            .Where(l => l.Id == input.LeaseId)
            .Select(l => (int?)l.PropertyManagerId)
            .FirstOrDefaultAsync(ct);
        if (leasePm is null)
        {
            throw new InvalidOperationException($"Lease {input.LeaseId} not found.");
        }
        if (leasePm != input.PropertyManagerId)
        {
            throw new InvalidOperationException(
                $"Lease {input.LeaseId} belongs to PM {leasePm}, not the caller PM {input.PropertyManagerId}.");
        }

        var hasActive = await db.PaymentPromises.AsNoTracking()
            .AnyAsync(p => p.LeaseId == input.LeaseId && p.Status == PromiseStatus.Active, ct);
        if (hasActive)
        {
            throw new InvalidOperationException(
                "This lease already has an active promise — cancel it before logging a new one.");
        }

        var promise = new PaymentPromise
        {
            PropertyManagerId = input.PropertyManagerId,
            LeaseId = input.LeaseId,
            Amount = input.Amount,
            PromisedDate = input.PromisedDate,
            Note = string.IsNullOrWhiteSpace(input.Note) ? null : input.Note.Trim(),
            Status = PromiseStatus.Active,
            CreatedAt = DateTime.UtcNow
        };

        db.PaymentPromises.Add(promise);
        await db.SaveChangesAsync(ct);
        return promise;
    }

    public async Task CancelAsync(int promiseId, int propertyManagerId, CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var promise = await db.PaymentPromises
            .FirstOrDefaultAsync(p => p.Id == promiseId && p.PropertyManagerId == propertyManagerId, ct)
            ?? throw new InvalidOperationException($"Promise {promiseId} not found.");

        if (promise.Status != PromiseStatus.Active)
        {
            throw new InvalidOperationException("Only an active promise can be cancelled.");
        }

        promise.Status = PromiseStatus.Cancelled;
        promise.ResolvedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task<PaymentPromise?> GetActiveForLeaseAsync(int leaseId, CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        return await db.PaymentPromises.AsNoTracking()
            .Where(p => p.LeaseId == leaseId && p.Status == PromiseStatus.Active)
            .OrderByDescending(p => p.Id)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<PaymentPromise>> ListForLeaseAsync(int leaseId, CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        return await db.PaymentPromises.AsNoTracking()
            .Where(p => p.LeaseId == leaseId)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<PaymentPromise>> ListRecentlyBrokenAsync(
        int propertyManagerId, DateOnly since, CancellationToken ct)
    {
        var sinceUtc = since.ToDateTime(TimeOnly.MinValue);
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        return await db.PaymentPromises.AsNoTracking()
            .Where(p => p.PropertyManagerId == propertyManagerId
                     && p.Status == PromiseStatus.Broken
                     && p.ResolvedAt >= sinceUtc)
            .OrderByDescending(p => p.ResolvedAt)
            .ToListAsync(ct);
    }

    public async Task<int> ResolveDueAsync(int propertyManagerId, DateOnly today, CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        // A promise is "due for resolution" the day AFTER its promised date —
        // the tenant has the whole promised day to pay.
        var due = await db.PaymentPromises
            .Where(p => p.PropertyManagerId == propertyManagerId
                     && p.Status == PromiseStatus.Active
                     && p.PromisedDate < today)
            .ToListAsync(ct);
        if (due.Count == 0) return 0;

        var broken = 0;
        foreach (var promise in due)
        {
            // Kept = nothing left open on invoices that were due on/before the
            // promised date. Invoices issued AFTER the promise (next month's
            // rent) don't count against it. We can only observe the synced
            // balance, not individual payments — so a partial payment that
            // leaves any of that balance open resolves as Broken.
            var stillOwed = await db.RentInvoices.AsNoTracking()
                .Where(i => i.LeaseId == promise.LeaseId
                         && i.Status == RentInvoiceStatus.Open
                         && i.DueDate <= promise.PromisedDate)
                .SumAsync(i => (decimal?)i.Balance, ct) ?? 0m;

            promise.Status = stillOwed <= 0m ? PromiseStatus.Kept : PromiseStatus.Broken;
            promise.ResolvedAt = DateTime.UtcNow;
            if (promise.Status == PromiseStatus.Broken) broken++;
        }

        await db.SaveChangesAsync(ct);
        return broken;
    }
}
