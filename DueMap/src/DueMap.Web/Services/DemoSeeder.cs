using System.Globalization;
using DueMap.Identity.Domain;
using DueMap.Tenancy.Domain;
using DueMap.Tenancy.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DueMap.Web.Services;

/// <summary>
/// Idempotent demo data. If <c>tenancy.property_managers.name = "Demo Property Management"</c>
/// already exists, the seeder exits without writing anything.
/// Otherwise it creates a complete, clickable demo:
/// <list type="bullet">
///   <item>Demo PM</item>
///   <item>Admin user <c>demo@duemap.dev</c> / <c>DemoUser1!</c></item>
///   <item>3 customers (one each in CA, TX, NY)</item>
///   <item>3 leases linked 1:1 to customers</item>
///   <item>3 open invoices with mixed due dates (past, today, future)</item>
/// </list>
/// Called from <c>Program.cs</c> only when <c>app.Environment.IsDevelopment()</c>.
/// </summary>
public sealed partial class DemoSeeder
{
    public const string DemoPmName = "Demo Property Management";
    public const string DemoUserEmail = "demo@duemap.dev";
    public const string DemoUserPassword = "DemoUser1!";

    private readonly TenancyDbContext _tenancy;
    private readonly UserManager<ApplicationUser> _users;
    private readonly ILogger<DemoSeeder> _logger;

    public DemoSeeder(
        TenancyDbContext tenancy,
        UserManager<ApplicationUser> users,
        ILogger<DemoSeeder> logger)
    {
        _tenancy = tenancy;
        _users = users;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken ct)
    {
        var existing = await _tenancy.PropertyManagers
            .FirstOrDefaultAsync(p => p.Name == DemoPmName, ct);
        if (existing is not null)
        {
            LogAlreadySeeded(_logger, existing.Id);
            return;
        }

        // ---- PM org ----
        var pm = new PropertyManager { Name = DemoPmName, CreatedAt = DateTime.UtcNow };
        _tenancy.PropertyManagers.Add(pm);
        await _tenancy.SaveChangesAsync(ct);

        // ---- Admin user (via UserManager for proper password hashing) ----
        var user = new ApplicationUser
        {
            UserName = DemoUserEmail,
            Email = DemoUserEmail,
            EmailConfirmed = true,
            PropertyManagerId = pm.Id,
            CreatedAt = DateTime.UtcNow
        };
        var userResult = await _users.CreateAsync(user, DemoUserPassword);
        if (!userResult.Succeeded)
        {
            throw new InvalidOperationException(
                "Demo seed failed to create user: " +
                string.Join("; ", userResult.Errors.Select(e => e.Description)));
        }

        // ---- State id lookups (must exist from rules.states + seed_state_rules.sql) ----
        var stateIds = await _tenancy.Database
            .SqlQuery<StateRow>($"SELECT id AS Id, code AS Code FROM rules.states WHERE code IN ('CA','TX','NY')")
            .ToDictionaryAsync(r => r.Code, r => r.Id, ct);

        if (stateIds.Count < 3)
        {
            throw new InvalidOperationException(
                "Demo seed needs CA/TX/NY rows in rules.states. Apply seed_state_rules.sql first.");
        }

        // ---- Customers ----
        var now = DateTime.UtcNow;
        var customers = new[]
        {
            new Customer { PropertyManagerId = pm.Id, ExternalProvider = ExternalAccountingProvider.QuickBooks,
                ExternalId = "demo-cust-1", DisplayName = "Alice Renter",   Email = "alice@example.com",
                Phone = "+15550000001", IsActive = true, LastSyncedAt = now },
            new Customer { PropertyManagerId = pm.Id, ExternalProvider = ExternalAccountingProvider.QuickBooks,
                ExternalId = "demo-cust-2", DisplayName = "Bob Tenant",     Email = "bob@example.com",
                Phone = "+15550000002", IsActive = true, LastSyncedAt = now },
            new Customer { PropertyManagerId = pm.Id, ExternalProvider = ExternalAccountingProvider.QuickBooks,
                ExternalId = "demo-cust-3", DisplayName = "Carla Occupant", Email = "carla@example.com",
                Phone = "+15550000003", IsActive = true, LastSyncedAt = now }
        };
        _tenancy.Customers.AddRange(customers);
        await _tenancy.SaveChangesAsync(ct);

        // ---- Leases (one per customer, mixed states) ----
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var leases = new[]
        {
            new Lease { PropertyManagerId = pm.Id, StateId = stateIds["CA"], CustomerId = customers[0].Id,
                MonthlyRent = 2100m, StartDate = today.AddMonths(-12), CreatedAt = now },
            new Lease { PropertyManagerId = pm.Id, StateId = stateIds["TX"], CustomerId = customers[1].Id,
                MonthlyRent = 1450m, StartDate = today.AddMonths(-8),  CreatedAt = now },
            new Lease { PropertyManagerId = pm.Id, StateId = stateIds["NY"], CustomerId = customers[2].Id,
                MonthlyRent = 2950m, StartDate = today.AddMonths(-6),  CreatedAt = now }
        };
        _tenancy.Leases.AddRange(leases);
        await _tenancy.SaveChangesAsync(ct);

        // ---- Invoices with mixed due dates ----
        //   #1: already past due (drives post-due path)
        //   #2: due today (drives due-date reminder)
        //   #3: due in 3 days (drives pre-due reminder when PM default is 3)
        var invoices = new[]
        {
            new RentInvoice { PropertyManagerId = pm.Id, CustomerId = customers[0].Id, LeaseId = leases[0].Id,
                ExternalProvider = ExternalAccountingProvider.QuickBooks, ExternalId = "demo-inv-1",
                ExternalDocNumber = "INV-DEMO-001",
                IssueDate = today.AddDays(-15), DueDate = today.AddDays(-7),
                TotalAmount = leases[0].MonthlyRent, Balance = leases[0].MonthlyRent,
                Currency = "USD", Status = RentInvoiceStatus.Open, LastSyncedAt = now },

            new RentInvoice { PropertyManagerId = pm.Id, CustomerId = customers[1].Id, LeaseId = leases[1].Id,
                ExternalProvider = ExternalAccountingProvider.QuickBooks, ExternalId = "demo-inv-2",
                ExternalDocNumber = "INV-DEMO-002",
                IssueDate = today.AddDays(-7), DueDate = today,
                TotalAmount = leases[1].MonthlyRent, Balance = leases[1].MonthlyRent,
                Currency = "USD", Status = RentInvoiceStatus.Open, LastSyncedAt = now },

            new RentInvoice { PropertyManagerId = pm.Id, CustomerId = customers[2].Id, LeaseId = leases[2].Id,
                ExternalProvider = ExternalAccountingProvider.QuickBooks, ExternalId = "demo-inv-3",
                ExternalDocNumber = "INV-DEMO-003",
                IssueDate = today.AddDays(-4), DueDate = today.AddDays(3),
                TotalAmount = leases[2].MonthlyRent, Balance = leases[2].MonthlyRent,
                Currency = "USD", Status = RentInvoiceStatus.Open, LastSyncedAt = now }
        };
        _tenancy.RentInvoices.AddRange(invoices);
        await _tenancy.SaveChangesAsync(ct);

        LogSeeded(_logger, pm.Id, DemoUserEmail);
    }

    private sealed record StateRow(int Id, string Code);

    [LoggerMessage(EventId = 9001, Level = LogLevel.Information,
        Message = "Demo data already seeded (PM id={PropertyManagerId}); skipping.")]
    static partial void LogAlreadySeeded(ILogger logger, int propertyManagerId);

    [LoggerMessage(EventId = 9002, Level = LogLevel.Information,
        Message = "Demo data seeded. PM id={PropertyManagerId}; sign in as {Email}.")]
    static partial void LogSeeded(ILogger logger, int propertyManagerId, string email);
}
