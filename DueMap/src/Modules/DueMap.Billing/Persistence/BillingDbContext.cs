using DueMap.Billing.Domain;
using Microsoft.EntityFrameworkCore;

namespace DueMap.Billing.Persistence;

/// <summary>
/// EF Core context over the <c>billing</c> schema. Cross-schema FKs to tenancy,
/// rules, and notices are stored as plain int/long columns — Billing does not
/// expose navigation properties into other modules' aggregates.
/// </summary>
public sealed class BillingDbContext : DbContext
{
    public BillingDbContext(DbContextOptions<BillingDbContext> options) : base(options) { }

    public DbSet<LateFeeAssessment> LateFeeAssessments => Set<LateFeeAssessment>();
    public DbSet<AssessmentRun>     AssessmentRuns     => Set<AssessmentRun>();
    public DbSet<PmProcessingRun>   PmProcessingRuns   => Set<PmProcessingRun>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("billing");

        modelBuilder.Entity<LateFeeAssessment>(e =>
        {
            e.ToTable("late_fee_assessments");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.LeaseId).HasColumnName("lease_id");
            e.Property(x => x.DueDate).HasColumnName("due_date");
            e.Property(x => x.AssessmentDate).HasColumnName("assessment_date");
            e.Property(x => x.FeeAmount).HasColumnName("fee_amount").HasColumnType("decimal(10,2)");
            e.Property(x => x.MonthlyRentSnapshot).HasColumnName("monthly_rent_snapshot").HasColumnType("decimal(10,2)");
            e.Property(x => x.StateRuleVersionId).HasColumnName("state_rule_version_id");
            e.Property(x => x.LocalRuleOverrideId).HasColumnName("local_rule_override_id");
            e.Property(x => x.Status)
                .HasColumnName("status")
                .HasMaxLength(20)
                .HasConversion(v => v.ToWire(), v => LateFeeAssessmentStatusMapping.FromWire(v));
            e.Property(x => x.ReversalReason).HasColumnName("reversal_reason").HasMaxLength(500);
            e.Property(x => x.ReversedAt).HasColumnName("reversed_at");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");

            e.HasIndex(x => new { x.LeaseId, x.DueDate }).IsUnique();
        });

        modelBuilder.Entity<AssessmentRun>(e =>
        {
            e.ToTable("assessment_runs");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.LeaseId).HasColumnName("lease_id");
            e.Property(x => x.DueDate).HasColumnName("due_date");
            e.Property(x => x.AssessmentDate).HasColumnName("assessment_date");
            e.Property(x => x.ActionKind)
                .HasColumnName("action_kind")
                .HasMaxLength(40)
                .HasConversion(v => v.ToWire(), v => ActionKindMapping.FromWire(v));
            e.Property(x => x.NoticeDeliveryId).HasColumnName("notice_delivery_id");
            e.Property(x => x.LateFeeAssessmentId).HasColumnName("late_fee_assessment_id");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");

            e.HasIndex(x => new { x.LeaseId, x.DueDate, x.ActionKind }).IsUnique();
        });

        modelBuilder.Entity<PmProcessingRun>(e =>
        {
            e.ToTable("pm_processing_runs");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.PropertyManagerId).HasColumnName("property_manager_id");
            e.Property(x => x.BusinessDate).HasColumnName("business_date");
            e.Property(x => x.StartedAt).HasColumnName("started_at");
            e.Property(x => x.CompletedAt).HasColumnName("completed_at");
            e.Property(x => x.Status)
                .HasColumnName("status")
                .HasMaxLength(20)
                .HasConversion(v => v.ToWire(), v => PmProcessingStatusMapping.FromWire(v));
            e.Property(x => x.FailureReason).HasColumnName("failure_reason");
            e.Property(x => x.LeasesPlanned).HasColumnName("leases_planned");
            e.Property(x => x.ActionsExecuted).HasColumnName("actions_executed");

            e.HasIndex(x => new { x.PropertyManagerId, x.BusinessDate }).IsUnique();
        });
    }
}
