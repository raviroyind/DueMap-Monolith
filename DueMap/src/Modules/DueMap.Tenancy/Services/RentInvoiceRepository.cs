using DueMap.Tenancy.Domain;
using DueMap.Tenancy.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DueMap.Tenancy.Services;

internal sealed class RentInvoiceRepository : IRentInvoiceRepository
{
    private readonly IDbContextFactory<TenancyDbContext> _dbFactory;

    public RentInvoiceRepository(IDbContextFactory<TenancyDbContext> dbFactory)
        => _dbFactory = dbFactory;

    public async Task<RentInvoice?> GetByExternalAsync(
        int propertyManagerId,
        ExternalAccountingProvider provider,
        string externalId,
        CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        return await db.RentInvoices.AsNoTracking()
            .FirstOrDefaultAsync(i => i.PropertyManagerId == propertyManagerId
                                   && i.ExternalProvider == provider
                                   && i.ExternalId == externalId, ct);
    }

    public async Task<DateOnly?> GetCurrentDueDateAsync(int leaseId, DateOnly asOf, CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        // Prefer the most recent already-due open invoice (the current rent period).
        // If everything's still in the future, fall back to the next open one.
        var pastOrToday = await db.RentInvoices.AsNoTracking()
            .Where(i => i.LeaseId == leaseId
                     && i.Status == RentInvoiceStatus.Open
                     && i.DueDate <= asOf)
            .OrderByDescending(i => i.DueDate)
            .Select(i => (DateOnly?)i.DueDate)
            .FirstOrDefaultAsync(ct);
        if (pastOrToday.HasValue) return pastOrToday;

        return await db.RentInvoices.AsNoTracking()
            .Where(i => i.LeaseId == leaseId
                     && i.Status == RentInvoiceStatus.Open
                     && i.DueDate > asOf)
            .OrderBy(i => i.DueDate)
            .Select(i => (DateOnly?)i.DueDate)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<string?> GetCurrentPayUrlAsync(int leaseId, DateOnly asOf, CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        // Same "current rent period" selection as GetCurrentDueDateAsync:
        // newest already-due open invoice first, fall back to next upcoming.
        var pastOrToday = await db.RentInvoices.AsNoTracking()
            .Where(i => i.LeaseId == leaseId
                     && i.Status == RentInvoiceStatus.Open
                     && i.DueDate <= asOf)
            .OrderByDescending(i => i.DueDate)
            .Select(i => i.PublicPaymentUrl)
            .FirstOrDefaultAsync(ct);
        if (!string.IsNullOrEmpty(pastOrToday)) return pastOrToday;

        return await db.RentInvoices.AsNoTracking()
            .Where(i => i.LeaseId == leaseId
                     && i.Status == RentInvoiceStatus.Open
                     && i.DueDate > asOf)
            .OrderBy(i => i.DueDate)
            .Select(i => i.PublicPaymentUrl)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<RentInvoiceExternalRef?> GetCurrentExternalAsync(int leaseId, DateOnly asOf, CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        // Same "current rent period" selection as GetCurrentDueDateAsync:
        // newest already-due open invoice first, fall back to next upcoming.
        var pastOrToday = await db.RentInvoices.AsNoTracking()
            .Where(i => i.LeaseId == leaseId
                     && i.Status == RentInvoiceStatus.Open
                     && i.DueDate <= asOf)
            .OrderByDescending(i => i.DueDate)
            .Select(i => new RentInvoiceExternalRef(i.ExternalProvider, i.ExternalId, i.ExternalDocNumber))
            .FirstOrDefaultAsync(ct);
        if (pastOrToday is not null) return pastOrToday;

        return await db.RentInvoices.AsNoTracking()
            .Where(i => i.LeaseId == leaseId
                     && i.Status == RentInvoiceStatus.Open
                     && i.DueDate > asOf)
            .OrderBy(i => i.DueDate)
            .Select(i => new RentInvoiceExternalRef(i.ExternalProvider, i.ExternalId, i.ExternalDocNumber))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<RentInvoice>> ListByPropertyManagerAsync(int propertyManagerId, CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        return await db.RentInvoices.AsNoTracking()
            .Where(i => i.PropertyManagerId == propertyManagerId)
            .OrderByDescending(i => i.DueDate)
            .ToListAsync(ct);
    }

    public async Task<RentInvoice?> GetByIdAsync(int invoiceId, CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        return await db.RentInvoices.AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == invoiceId, ct);
    }

    public async Task<RentInvoice> UpsertAsync(RentInvoice invoice, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(invoice);
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var existing = await db.RentInvoices.FirstOrDefaultAsync(
            i => i.PropertyManagerId == invoice.PropertyManagerId
              && i.ExternalProvider == invoice.ExternalProvider
              && i.ExternalId == invoice.ExternalId, ct);

        var now = DateTime.UtcNow;

        if (existing is null)
        {
            invoice.CreatedAt = now;
            invoice.UpdatedAt = now;
            if (invoice.LastSyncedAt == default) invoice.LastSyncedAt = now;
            db.RentInvoices.Add(invoice);
            await db.SaveChangesAsync(ct);
            return invoice;
        }

        existing.CustomerId        = invoice.CustomerId;
        existing.LeaseId           = invoice.LeaseId ?? existing.LeaseId;   // preserve a manual link
        existing.ExternalDocNumber = invoice.ExternalDocNumber;
        existing.IssueDate         = invoice.IssueDate;
        existing.DueDate           = invoice.DueDate;
        existing.TotalAmount       = invoice.TotalAmount;
        existing.Balance           = invoice.Balance;
        existing.Currency          = invoice.Currency;
        existing.Status            = invoice.Status;
        existing.PublicPaymentUrl  = invoice.PublicPaymentUrl;
        existing.LastSyncedAt      = invoice.LastSyncedAt == default ? now : invoice.LastSyncedAt;
        existing.UpdatedAt         = now;
        await db.SaveChangesAsync(ct);
        return existing;
    }
}
