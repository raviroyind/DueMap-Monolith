using DueMap.Tenancy.Domain;
using Microsoft.EntityFrameworkCore;

namespace DueMap.Tenancy.Persistence;

public sealed class TenancyDbContext : DbContext
{
    public TenancyDbContext(DbContextOptions<TenancyDbContext> options) : base(options) { }

    public DbSet<PropertyManager>      PropertyManagers     => Set<PropertyManager>();
    public DbSet<Lease>                Leases               => Set<Lease>();
    public DbSet<PmNoticePreferences>  PmNoticePreferences  => Set<PmNoticePreferences>();
    public DbSet<LeaseNoticeSettings>  LeaseNoticeSettings  => Set<LeaseNoticeSettings>();
    public DbSet<PmAccountingDefaults> PmAccountingDefaults => Set<PmAccountingDefaults>();
    public DbSet<Customer>             Customers            => Set<Customer>();
    public DbSet<RentInvoice>          RentInvoices         => Set<RentInvoice>();
    public DbSet<PmDailyCloseSettings> PmDailyCloseSettings => Set<PmDailyCloseSettings>();
    public DbSet<TenantLogin>          TenantLogins         => Set<TenantLogin>();
    public DbSet<TenantMagicLink>      TenantMagicLinks     => Set<TenantMagicLink>();
    public DbSet<TenantSession>        TenantSessions       => Set<TenantSession>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("tenancy");

        modelBuilder.Entity<PropertyManager>(e =>
        {
            e.ToTable("property_managers");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
            e.Property(x => x.OnboardingStatus)
                .HasColumnName("onboarding_status")
                .HasConversion<int>();   // store as int; the enum gives us type safety in code
            e.Property(x => x.TimeZoneId).HasColumnName("time_zone_id").HasMaxLength(64).IsRequired();
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            // v12 + v13 onboarding step timestamps — nullable, set when each step completes.
            e.Property(x => x.StepConnectDoneAt).HasColumnName("step_connect_done_at");
            e.Property(x => x.StepCloseDoneAt).HasColumnName("step_close_done_at");
            e.Property(x => x.StepNoticePrefsDoneAt).HasColumnName("step_notice_prefs_done_at"); // v13
            e.Property(x => x.StepPreflightDoneAt).HasColumnName("step_preflight_done_at");
            // v20 (P1-3) — AutoSetup output.
            e.Property(x => x.AutoSetupSummary).HasColumnName("auto_setup_summary");
            e.Property(x => x.AutoSetupDoneAt).HasColumnName("auto_setup_done_at");
            e.Ignore(x => x.OnboardingComplete);   // computed in code, not persisted
        });

        modelBuilder.Entity<Lease>(e =>
        {
            e.ToTable("leases");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.PropertyManagerId).HasColumnName("property_manager_id");
            e.Property(x => x.StateId).HasColumnName("state_id");
            e.Property(x => x.JurisdictionId).HasColumnName("jurisdiction_id");
            e.Property(x => x.CustomerId).HasColumnName("customer_id");
            e.Property(x => x.MonthlyRent).HasColumnName("monthly_rent").HasColumnType("decimal(10,2)");
            e.Property(x => x.StartDate).HasColumnName("start_date");
            e.Property(x => x.EndDate).HasColumnName("end_date");
            e.Property(x => x.UnitLabel).HasColumnName("unit_label").HasMaxLength(50);
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            // v18 (P1-1) — auto-discovery columns.
            e.Property(x => x.InferredRentAmount).HasColumnName("inferred_rent_amount").HasColumnType("decimal(18,2)");
            e.Property(x => x.InferredDueDay).HasColumnName("inferred_due_day");
            e.Property(x => x.InferredState).HasColumnName("inferred_state").HasMaxLength(2).IsFixedLength();
            e.Property(x => x.InferredAt).HasColumnName("inferred_at");
            e.Property(x => x.DiscoveryConfirmedAt).HasColumnName("discovery_confirmed_at");
            // v20 (P1-3) — AutoSetup-written late-fee profile + staging gate.
            e.Property(x => x.LateFeeType).HasColumnName("late_fee_type");
            e.Property(x => x.LateFeePercent).HasColumnName("late_fee_percent").HasColumnType("decimal(5,2)");
            e.Property(x => x.LateFeeFlatAmount).HasColumnName("late_fee_flat_amount").HasColumnType("decimal(10,2)");
            e.Property(x => x.LateFeeGraceDays).HasColumnName("late_fee_grace_days");
            e.Property(x => x.LateFeeDailyAccrual).HasColumnName("late_fee_daily_accrual");
            e.Property(x => x.FeesStaged).HasColumnName("fees_staged");
            // v24 (P2-1) — autopay status.
            e.Property(x => x.AutopayStatus).HasColumnName("autopay_status").HasConversion<byte>();
            e.Property(x => x.AutopayCheckedAt).HasColumnName("autopay_checked_at");
        });

        modelBuilder.Entity<PmNoticePreferences>(e =>
        {
            e.ToTable("pm_notice_preferences");
            e.HasKey(x => x.PropertyManagerId);
            e.Property(x => x.PropertyManagerId).HasColumnName("property_manager_id").ValueGeneratedNever();
            e.Property(x => x.PreDueMasterEnabled).HasColumnName("pre_due_master_enabled");
            e.Property(x => x.PreDueDefaultDaysBefore).HasColumnName("pre_due_default_days_before");
            e.Property(x => x.DueDateMasterEnabled).HasColumnName("due_date_master_enabled");
            e.Property(x => x.PostDueMasterEnabled).HasColumnName("post_due_master_enabled");
            e.Property(x => x.PostDueDefaultMode)
                .HasColumnName("post_due_default_mode")
                .HasMaxLength(20)
                .HasConversion(v => v.ToWire(), v => PostDueModeMapping.FromWire(v));
            e.Property(x => x.PostDueDefaultGraceDays).HasColumnName("post_due_default_grace_days");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        });

        modelBuilder.Entity<LeaseNoticeSettings>(e =>
        {
            e.ToTable("lease_notice_settings");
            e.HasKey(x => x.LeaseId);
            e.Property(x => x.LeaseId).HasColumnName("lease_id").ValueGeneratedNever();
            e.Property(x => x.PreDueEnabled).HasColumnName("pre_due_enabled");
            e.Property(x => x.PreDueDaysBefore).HasColumnName("pre_due_days_before");
            e.Property(x => x.DueDateEnabled).HasColumnName("due_date_enabled");
            e.Property(x => x.PostDueEnabled).HasColumnName("post_due_enabled");
            e.Property(x => x.PostDueMode)
                .HasColumnName("post_due_mode")
                .HasMaxLength(20)
                .HasConversion(
                    v => v.HasValue ? v.Value.ToWire() : null,
                    v => v == null ? (PostDueMode?)null : PostDueModeMapping.FromWire(v));
            e.Property(x => x.PostDueGraceDays).HasColumnName("post_due_grace_days");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        });

        modelBuilder.Entity<PmAccountingDefaults>(e =>
        {
            e.ToTable("pm_accounting_defaults");
            e.HasKey(x => x.PropertyManagerId);
            e.Property(x => x.PropertyManagerId).HasColumnName("property_manager_id").ValueGeneratedNever();
            e.Property(x => x.DefaultCurrency).HasColumnName("default_currency").HasMaxLength(3).IsFixedLength().IsRequired();
            e.Property(x => x.DefaultPaymentTermsDays).HasColumnName("default_payment_terms_days");
            e.Property(x => x.DefaultLateFeeItemExternalId).HasColumnName("default_late_fee_item_external_id").HasMaxLength(200);
            e.Property(x => x.TimezoneId).HasColumnName("timezone_id").HasMaxLength(60).IsRequired();
            e.Property(x => x.LastSyncedAt).HasColumnName("last_synced_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        });

        modelBuilder.Entity<Customer>(e =>
        {
            e.ToTable("customers");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.PropertyManagerId).HasColumnName("property_manager_id");
            e.Property(x => x.ExternalProvider)
                .HasColumnName("external_provider")
                .HasMaxLength(20)
                .HasConversion(v => v.ToWire(), v => ExternalAccountingProviderMapping.FromWire(v));
            e.Property(x => x.ExternalId).HasColumnName("external_id").HasMaxLength(200).IsRequired();
            e.Property(x => x.DisplayName).HasColumnName("display_name").HasMaxLength(400).IsRequired();
            e.Property(x => x.Email).HasColumnName("email").HasMaxLength(400);
            e.Property(x => x.Phone).HasColumnName("phone").HasMaxLength(50);
            e.Property(x => x.IsActive).HasColumnName("is_active");
            e.Property(x => x.LastSyncedAt).HasColumnName("last_synced_at");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            e.Property(x => x.PreflightNotifiedAt).HasColumnName("preflight_notified_at");
            // v18 (P1-1) — billing-state from QBO BillAddr / Xero Address.Region.
            e.Property(x => x.BillingState).HasColumnName("billing_state").HasMaxLength(2).IsFixedLength();
            e.HasIndex(x => new { x.PropertyManagerId, x.ExternalProvider, x.ExternalId }).IsUnique();
        });

        modelBuilder.Entity<RentInvoice>(e =>
        {
            e.ToTable("rent_invoices");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.PropertyManagerId).HasColumnName("property_manager_id");
            e.Property(x => x.CustomerId).HasColumnName("customer_id");
            e.Property(x => x.LeaseId).HasColumnName("lease_id");
            e.Property(x => x.ExternalProvider)
                .HasColumnName("external_provider")
                .HasMaxLength(20)
                .HasConversion(v => v.ToWire(), v => ExternalAccountingProviderMapping.FromWire(v));
            e.Property(x => x.ExternalId).HasColumnName("external_id").HasMaxLength(200).IsRequired();
            e.Property(x => x.ExternalDocNumber).HasColumnName("external_doc_number").HasMaxLength(100);
            e.Property(x => x.IssueDate).HasColumnName("issue_date");
            e.Property(x => x.DueDate).HasColumnName("due_date");
            e.Property(x => x.TotalAmount).HasColumnName("total_amount").HasColumnType("decimal(10,2)");
            e.Property(x => x.Balance).HasColumnName("balance").HasColumnType("decimal(10,2)");
            e.Property(x => x.Currency).HasColumnName("currency").HasMaxLength(3).IsFixedLength().IsRequired();
            e.Property(x => x.Status)
                .HasColumnName("status")
                .HasMaxLength(20)
                .HasConversion(v => v.ToWire(), v => RentInvoiceStatusMapping.FromWire(v));
            e.Property(x => x.PublicPaymentUrl).HasColumnName("public_payment_url").HasMaxLength(500);
            e.Property(x => x.LastSyncedAt).HasColumnName("last_synced_at");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            e.HasIndex(x => new { x.PropertyManagerId, x.ExternalProvider, x.ExternalId }).IsUnique();
        });

        modelBuilder.Entity<PmDailyCloseSettings>(e =>
        {
            e.ToTable("pm_daily_close_settings");
            e.HasKey(x => x.PropertyManagerId);
            e.Property(x => x.PropertyManagerId).HasColumnName("property_manager_id").ValueGeneratedNever();
            e.Property(x => x.Enabled).HasColumnName("enabled");
            e.Property(x => x.SendHourLocal).HasColumnName("send_hour_local");
            e.Property(x => x.TimeZoneId).HasColumnName("time_zone_id").HasMaxLength(64).IsRequired();
            e.Property(x => x.RecipientOverride).HasColumnName("recipient_override").HasMaxLength(256);
            e.Property(x => x.CcList).HasColumnName("cc_list").HasMaxLength(1024);
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        });

        // v15 — tenant portal
        modelBuilder.Entity<TenantLogin>(e =>
        {
            e.ToTable("tenant_logins");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.PropertyManagerId).HasColumnName("property_manager_id");
            e.Property(x => x.CustomerId).HasColumnName("customer_id");
            e.Property(x => x.Email).HasColumnName("email").HasMaxLength(400).IsRequired();
            e.Property(x => x.PasswordHash).HasColumnName("password_hash").HasMaxLength(500);
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.LastLoginAt).HasColumnName("last_login_at");
            e.HasIndex(x => new { x.PropertyManagerId, x.Email }).IsUnique();
        });

        modelBuilder.Entity<TenantMagicLink>(e =>
        {
            e.ToTable("tenant_magic_links");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.TokenHash).HasColumnName("token_hash").HasMaxLength(128).IsRequired();
            e.Property(x => x.TenantLoginId).HasColumnName("tenant_login_id");
            e.Property(x => x.RequestedAt).HasColumnName("requested_at");
            e.Property(x => x.ExpiresAt).HasColumnName("expires_at");
            e.Property(x => x.ConsumedAt).HasColumnName("consumed_at");
            e.Property(x => x.RequestIp).HasColumnName("request_ip").HasMaxLength(64);
            e.Property(x => x.ConsumeIp).HasColumnName("consume_ip").HasMaxLength(64);
            e.HasIndex(x => x.TokenHash).IsUnique();
        });

        modelBuilder.Entity<TenantSession>(e =>
        {
            e.ToTable("tenant_sessions");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.CookieHash).HasColumnName("cookie_hash").HasMaxLength(128).IsRequired();
            e.Property(x => x.TenantLoginId).HasColumnName("tenant_login_id");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.LastSeenAt).HasColumnName("last_seen_at");
            e.Property(x => x.ExpiresAt).HasColumnName("expires_at");
            e.Property(x => x.RevokedAt).HasColumnName("revoked_at");
            e.Property(x => x.CreatedIp).HasColumnName("created_ip").HasMaxLength(64);
            // Filtered unique index lives in the SQL migration — EF doesn't
            // model SQL Server filtered indexes well in OnModelCreating.
        });
    }
}
