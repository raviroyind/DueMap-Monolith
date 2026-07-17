using System.Globalization;
using DueMap.Billing;
using DueMap.Notices;
using DueMap.Notices.Domain;
using DueMap.Tenancy;
using DueMap.Tenancy.Domain;

namespace DueMap.Web.Services;

public enum TodayPriority
{
    DeliveryFailed = 1,   // tenant legally may not have been noticed
    PromiseBroken  = 2,   // trust event — call before escalating further
    FeeImminent    = 3,   // "call them personally before the fee posts"
    AutopayProblem = 4,   // autopay was supposed to cover this
    LeaseExpiring  = 5
}

/// <summary>One actionable row in the Today queue.</summary>
public sealed record TodayItem(
    string Key,               // deterministic — dismissals persist against it
    TodayPriority Priority,
    string Category,          // short chip label
    string Title,
    string Detail,
    int? LeaseId,
    bool OfferPromise);       // show the "Log promise" quick action

/// <summary>
/// Aggregates "what needs the PM at 8am" from data other features already
/// maintain — deliveries, promises, invoices, lease dates. No new plumbing;
/// this is the daily-close report turned into a WORK QUEUE: every row is
/// actionable or dismissable. Ordered by how much trouble ignoring it causes.
/// </summary>
public sealed class TodayQueueService
{
    private const int DeliveryLookbackDays = 7;
    private const int BrokenPromiseLookbackDays = 14;
    private const int FeeImminentWindowDays = 2;    // fee posts within this many days
    private const int LeaseExpiryWindowDays = 60;

    private readonly ILeaseReader _leases;
    private readonly IRentInvoiceRepository _invoices;
    private readonly ICustomerRepository _customers;
    private readonly IPaymentPromiseService _promises;
    private readonly IWorkQueueDismissalService _dismissals;
    private readonly INoticeDeliveryRepository _deliveries;
    private readonly IEffectivePolicyService _policy;

    public TodayQueueService(
        ILeaseReader leases,
        IRentInvoiceRepository invoices,
        ICustomerRepository customers,
        IPaymentPromiseService promises,
        IWorkQueueDismissalService dismissals,
        INoticeDeliveryRepository deliveries,
        IEffectivePolicyService policy)
    {
        _leases = leases;
        _invoices = invoices;
        _customers = customers;
        _promises = promises;
        _dismissals = dismissals;
        _deliveries = deliveries;
        _policy = policy;
    }

    public async Task<IReadOnlyList<TodayItem>> BuildAsync(int pmId, DateOnly today, CancellationToken ct)
    {
        // Resolve promises FIRST so "broke yesterday" is visible at 8am even
        // if the worker hasn't ticked for this PM yet. Idempotent.
        await _promises.ResolveDueAsync(pmId, today, ct);

        var dismissed = await _dismissals.ListKeysAsync(pmId, ct);

        var leases = await _leases.ListActiveAsync(pmId, today, ct);
        var leaseById = leases.ToDictionary(l => l.Id);
        var allInvoices = await _invoices.ListByPropertyManagerAsync(pmId, ct);
        var customerNames = (await _customers.ListByPropertyManagerAsync(pmId, ct))
            .ToDictionary(c => c.Id, c => c.DisplayName);

        string TenantName(int? leaseId) =>
            leaseId is int lid
            && leaseById.TryGetValue(lid, out var l)
            && l.CustomerId is int cid
            && customerNames.TryGetValue(cid, out var name)
                ? name
                : $"Lease #{leaseId}";

        // Oldest open invoice per lease drives days-past-due, same rule the
        // lease detail page uses.
        var oldestOpenByLease = allInvoices
            .Where(i => i.LeaseId is not null && i.Status == RentInvoiceStatus.Open)
            .GroupBy(i => i.LeaseId!.Value)
            .ToDictionary(g => g.Key, g => g.OrderBy(i => i.DueDate).First());

        var items = new List<TodayItem>();

        // 1 — Failed / bounced notice deliveries. A tenant who never received
        //     the notice legally may not have been noticed at all.
        var nowUtc = DateTime.UtcNow;
        var recentDeliveries = await _deliveries.ListSentInRangeForPmAsync(
            pmId, nowUtc.AddDays(-DeliveryLookbackDays), nowUtc, ct);
        foreach (var d in recentDeliveries.Where(d =>
                     d.Status is DeliveryStatus.Failed or DeliveryStatus.Bounced))
        {
            items.Add(new TodayItem(
                Key: $"delivery:{d.Id}",
                Priority: TodayPriority.DeliveryFailed,
                Category: d.Status == DeliveryStatus.Bounced ? "Bounced" : "Send failed",
                Title: $"{TenantName(d.LeaseId)} — notice not delivered",
                Detail: $"\"{d.RenderedSubject}\" ({(d.Channel == NoticeChannel.Email ? "email" : "SMS")}, {d.SentAt:MMM d}). " +
                        "They may not have been legally noticed — fix the contact info or reach them another way.",
                LeaseId: d.LeaseId,
                OfferPromise: false));
        }

        // 2 — Promises broken recently. The verbal agreement failed; the
        //     machine has resumed, but a human call usually beats a template.
        var broken = await _promises.ListRecentlyBrokenAsync(
            pmId, today.AddDays(-BrokenPromiseLookbackDays), ct);
        foreach (var p in broken)
        {
            var amount = p.Amount is decimal a ? a.ToString("N2", CultureInfo.InvariantCulture) : "the balance";
            items.Add(new TodayItem(
                Key: $"promise:{p.Id}",
                Priority: TodayPriority.PromiseBroken,
                Category: "Promise broken",
                Title: $"{TenantName(p.LeaseId)} — promised {amount} by {p.PromisedDate:MMM d} and didn't pay",
                Detail: $"Escalation resumed automatically{(string.IsNullOrEmpty(p.Note) ? "" : $" · \"{p.Note}\"")}. " +
                        "Call before the next notice lands, or log a new promise.",
                LeaseId: p.LeaseId,
                OfferPromise: true));
        }

        // 3 — Late fee posts within the next couple of days: the last window
        //     for a personal call. 4 — Autopay problems on overdue leases.
        foreach (var lease in leases)
        {
            if (!oldestOpenByLease.TryGetValue(lease.Id, out var oldestOpen)) continue;
            var daysPastDue = today.DayNumber - oldestOpen.DueDate.DayNumber;
            if (daysPastDue <= 0) continue;

            if (lease.AutopayStatus == AutopayStatus.Enrolled)
            {
                items.Add(new TodayItem(
                    Key: $"autopay:{lease.Id}:{oldestOpen.DueDate:yyyyMMdd}",
                    Priority: TodayPriority.AutopayProblem,
                    Category: "Autopay",
                    Title: $"{TenantName(lease.Id)} — on autopay but {daysPastDue} day(s) overdue",
                    Detail: string.Create(CultureInfo.InvariantCulture,
                        $"Balance {oldestOpen.Balance:N2} due {oldestOpen.DueDate:MMM d}. Their autopay likely failed — worth a heads-up before reminders escalate."),
                    LeaseId: lease.Id,
                    OfferPromise: true));
            }

            // Paused-by-promise leases aren't imminent — the pause is the plan.
            if (lease.FeesStaged) continue;
            var activePromise = await _promises.GetActiveForLeaseAsync(lease.Id, ct);
            if (activePromise is not null && today <= activePromise.PromisedDate) continue;

            var policy = await _policy.BuildAsync(lease, today, ct);
            if (!policy.PostDueEnabled) continue;

            var feePostsOnDay = policy.PostDueMode == PostDueMode.GracePeriod
                ? policy.EffectiveGraceDays + 1
                : policy.StateMinimumGraceDays + 1;

            if (daysPastDue < feePostsOnDay && daysPastDue >= feePostsOnDay - FeeImminentWindowDays)
            {
                var daysLeft = feePostsOnDay - daysPastDue;
                items.Add(new TodayItem(
                    Key: $"feerisk:{lease.Id}:{oldestOpen.DueDate:yyyyMMdd}",
                    Priority: TodayPriority.FeeImminent,
                    Category: "Fee imminent",
                    Title: $"{TenantName(lease.Id)} — late-fee warning goes out in {daysLeft} day{(daysLeft == 1 ? "" : "s")}",
                    Detail: string.Create(CultureInfo.InvariantCulture,
                        $"{daysPastDue} day(s) past due, balance {oldestOpen.Balance:N2}. Last window to call them personally or log a promise."),
                    LeaseId: lease.Id,
                    OfferPromise: true));
            }
        }

        // 5 — Leases ending within 60 days: renewal conversations start now.
        foreach (var lease in leases)
        {
            if (lease.EndDate is not DateOnly end) continue;
            var daysToEnd = end.DayNumber - today.DayNumber;
            if (daysToEnd < 0 || daysToEnd > LeaseExpiryWindowDays) continue;

            items.Add(new TodayItem(
                Key: $"expiry:{lease.Id}:{end:yyyyMMdd}",
                Priority: TodayPriority.LeaseExpiring,
                Category: "Expiring",
                Title: $"{TenantName(lease.Id)} — lease ends {end:MMM d} ({daysToEnd} days)",
                Detail: "Start the renewal (or non-renewal notice) conversation.",
                LeaseId: lease.Id,
                OfferPromise: false));
        }

        return items
            .Where(i => !dismissed.Contains(i.Key))
            .OrderBy(i => i.Priority)
            .ThenBy(i => i.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
