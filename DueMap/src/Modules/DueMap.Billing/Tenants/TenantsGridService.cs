using DueMap.Rules;
using DueMap.Tenancy;
using DueMap.Tenancy.Domain;

namespace DueMap.Billing.Tenants;

internal sealed class TenantsGridService : ITenantsGridService
{
    private readonly ICustomerRepository _customers;
    private readonly ILeaseReader _leases;
    private readonly ILeaseWriter _leaseWriter;
    private readonly IStateReader _states;
    private readonly IRulesService _rules;

    public TenantsGridService(
        ICustomerRepository customers,
        ILeaseReader leases,
        ILeaseWriter leaseWriter,
        IStateReader states,
        IRulesService rules)
    {
        _customers = customers;
        _leases = leases;
        _leaseWriter = leaseWriter;
        _states = states;
        _rules = rules;
    }

    public async Task<IReadOnlyList<TenantRow>> GetRowsAsync(int propertyManagerId, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var customers = await _customers.ListByPropertyManagerAsync(propertyManagerId, ct);
        var leases    = await _leases.ListActiveAsync(propertyManagerId, today, ct);
        var states    = await _states.ListAllAsync(ct);
        var stateById = states.ToDictionary(s => s.Id);

        // First active lease per customer. A customer with 2+ leases is rare
        // (and ambiguous for the grid) — we show the first and leave the rest
        // to the lease drill-down. Unlinked leases (CustomerId null) don't
        // appear here; the screen is customer-centric by design.
        var leaseByCustomer = leases
            .Where(l => l.CustomerId is not null)
            .GroupBy(l => l.CustomerId!.Value)
            .ToDictionary(g => g.Key, g => g.First());

        // Resolve the state cap once per distinct state (RulesService caches).
        var maxPctByState = new Dictionary<int, decimal?>();
        foreach (var stateId in leaseByCustomer.Values.Select(l => l.StateId).Distinct())
        {
            var rule = await _rules.ResolveRuleByStateIdAsync(stateId, today, jurisdictionId: null, ct);
            maxPctByState[stateId] = rule?.StateMaxPercent;
        }

        var rows = new List<TenantRow>(customers.Count);
        foreach (var c in customers)
        {
            leaseByCustomer.TryGetValue(c.Id, out var lease);

            int? stateId = lease?.StateId;
            string? stateCode = stateId is int sid && stateById.TryGetValue(sid, out var st) ? st.Code : null;
            var stateKnown = lease is not null && stateCode is not null;
            decimal? maxPct = stateId is int s2 && maxPctByState.TryGetValue(s2, out var mp) ? mp : null;

            var risk = lease is null
                ? FeeRisk.Unknown
                : FeeRiskClassifier.Classify(lease.LateFeePercent, maxPct, stateKnown);

            rows.Add(new TenantRow(
                CustomerId:      c.Id,
                CustomerName:    c.DisplayName,
                Email:           c.Email,
                BillingState:    c.BillingState,
                LeaseId:         lease?.Id,
                MonthlyRent:     lease?.MonthlyRent,
                StateId:         stateId,
                StateCode:       stateCode,
                LateFeePercent:  lease?.LateFeePercent,
                FeesStaged:      lease?.FeesStaged ?? false,
                // Linked leases get reminders by default; the per-channel toggle
                // lives on the lease drill-down. Bulk reminder editing is a
                // follow-up (see P1-5 deferral notes).
                RemindersEnabled: lease is not null,
                FeeRisk:         risk,
                AutopayStatus:   lease?.AutopayStatus switch     // P2-1
                {
                    DueMap.Tenancy.Domain.AutopayStatus.Enrolled => "Autopay",
                    DueMap.Tenancy.Domain.AutopayStatus.None     => "Manual",
                    _                                            => "—"
                }));
        }

        return rows;
    }

    public Task UpdateLeaseAsync(int leaseId, decimal monthlyRent, int stateId, CancellationToken ct) =>
        _leaseWriter.UpdateCoreFieldsAsync(leaseId, monthlyRent, stateId, ct);

    public async Task<int> CreateLeaseForCustomerAsync(
        int propertyManagerId, int customerId, int stateId, decimal monthlyRent, CancellationToken ct)
    {
        var lease = await _leaseWriter.CreateAsync(new NewLeaseInput(
            PropertyManagerId: propertyManagerId,
            StateId:           stateId,
            CustomerId:        customerId,
            MonthlyRent:       monthlyRent,
            StartDate:         DateOnly.FromDateTime(DateTime.UtcNow),
            EndDate:           null), ct);
        return lease.Id;
    }

    public async Task<IReadOnlyList<(int Id, string Code, string Name)>> GetStatesAsync(CancellationToken ct)
    {
        var states = await _states.ListAllAsync(ct);
        return states.Select(s => (s.Id, s.Code, s.Name)).ToList();
    }
}
