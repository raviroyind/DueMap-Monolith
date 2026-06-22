using DueMap.Rules.Domain;
using Microsoft.EntityFrameworkCore;

namespace DueMap.Rules.Persistence;

/// <summary>
/// EF Core context over the <c>rules</c> schema. The schema is owned by the
/// SQL DDL in <c>db/duemap_schema.sql</c> — this context maps to existing
/// tables, it does not own migrations for them. Treat the SQL script as the
/// source of truth.
/// </summary>
public sealed class RulesDbContext : DbContext
{
    public RulesDbContext(DbContextOptions<RulesDbContext> options) : base(options) { }

    public DbSet<State>               States              => Set<State>();
    public DbSet<StateRuleVersion>    StateRuleVersions   => Set<StateRuleVersion>();
    public DbSet<LocalJurisdiction>   LocalJurisdictions  => Set<LocalJurisdiction>();
    public DbSet<LocalRuleOverride>   LocalRuleOverrides  => Set<LocalRuleOverride>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("rules");

        modelBuilder.Entity<State>(e =>
        {
            e.ToTable("states");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.Code).HasColumnName("code").HasMaxLength(2).IsRequired();
            e.Property(x => x.Name).HasColumnName("name").HasMaxLength(80).IsRequired();
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.HasIndex(x => x.Code).IsUnique();
        });

        modelBuilder.Entity<StateRuleVersion>(e =>
        {
            e.ToTable("state_rule_versions");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.StateId).HasColumnName("state_id");
            e.Property(x => x.EffectiveDate).HasColumnName("effective_date");
            e.Property(x => x.ExpiresDate).HasColumnName("expires_date");
            e.Property(x => x.GracePeriodDays).HasColumnName("grace_period_days");
            e.Property(x => x.LateFeeType)
                .HasColumnName("late_fee_type")
                .HasMaxLength(20)
                .HasConversion(v => v.ToWire(), v => LateFeeTypeMapping.FromWire(v));
            e.Property(x => x.FlatAmount).HasColumnName("flat_amount").HasColumnType("decimal(10,2)");
            e.Property(x => x.PercentOfRent).HasColumnName("percent_of_rent").HasColumnType("decimal(5,4)");
            e.Property(x => x.HardCapAmount).HasColumnName("hard_cap_amount").HasColumnType("decimal(10,2)");
            e.Property(x => x.NoticeRequiredBeforeFee).HasColumnName("notice_required_before_fee");
            e.Property(x => x.NoticeAdvanceDays).HasColumnName("notice_advance_days");
            e.Property(x => x.SourceCitation).HasColumnName("source_citation").HasMaxLength(500).IsRequired();
            e.Property(x => x.Notes).HasColumnName("notes");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            // v19 (P1-2) — §6.1 guardrail columns.
            e.Property(x => x.MaxPercent).HasColumnName("max_percent").HasColumnType("decimal(5,2)");
            e.Property(x => x.MaxFlatAmount).HasColumnName("max_flat_amount").HasColumnType("decimal(10,2)");
            e.Property(x => x.MinGraceDays).HasColumnName("min_grace_days");
            e.Property(x => x.DailyAccrualOk).HasColumnName("daily_accrual_ok");
            e.Property(x => x.RequiresWrittenDisclosure).HasColumnName("requires_written_disclosure");
            e.Property(x => x.StandardKind).HasColumnName("standard_kind");
            e.Property(x => x.SafeDefaultPct).HasColumnName("safe_default_pct").HasColumnType("decimal(5,2)");
            e.Property(x => x.PlainSummary).HasColumnName("plain_summary").HasMaxLength(600);
            e.Property(x => x.SourceUrl).HasColumnName("source_url").HasMaxLength(400);
        });

        modelBuilder.Entity<LocalJurisdiction>(e =>
        {
            e.ToTable("local_jurisdictions");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.StateId).HasColumnName("state_id");
            e.Property(x => x.Name).HasColumnName("name").HasMaxLength(120).IsRequired();
            e.Property(x => x.JurisdictionType).HasColumnName("jurisdiction_type").HasMaxLength(20).IsRequired();
            e.Property(x => x.FipsCode).HasColumnName("fips_code").HasMaxLength(10);
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
        });

        modelBuilder.Entity<LocalRuleOverride>(e =>
        {
            e.ToTable("local_rule_overrides");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.JurisdictionId).HasColumnName("jurisdiction_id");
            e.Property(x => x.EffectiveDate).HasColumnName("effective_date");
            e.Property(x => x.ExpiresDate).HasColumnName("expires_date");
            e.Property(x => x.GracePeriodDays).HasColumnName("grace_period_days");
            e.Property(x => x.FlatAmount).HasColumnName("flat_amount").HasColumnType("decimal(10,2)");
            e.Property(x => x.PercentOfRent).HasColumnName("percent_of_rent").HasColumnType("decimal(5,4)");
            e.Property(x => x.HardCapAmount).HasColumnName("hard_cap_amount").HasColumnType("decimal(10,2)");
            e.Property(x => x.NoticeAdvanceDays).HasColumnName("notice_advance_days");
            e.Property(x => x.SourceCitation).HasColumnName("source_citation").HasMaxLength(500).IsRequired();
            e.Property(x => x.Notes).HasColumnName("notes");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
        });
    }
}
