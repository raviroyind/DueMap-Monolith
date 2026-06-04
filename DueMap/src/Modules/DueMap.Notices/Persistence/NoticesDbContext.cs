using DueMap.Notices.Domain;
using Microsoft.EntityFrameworkCore;

namespace DueMap.Notices.Persistence;

/// <summary>
/// EF Core context over the <c>notices</c> schema. The schema is owned by
/// <c>db/duemap_schema.sql</c> — this context maps to existing tables.
/// </summary>
public sealed class NoticesDbContext : DbContext
{
    public NoticesDbContext(DbContextOptions<NoticesDbContext> options) : base(options) { }

    public DbSet<NoticeType>            NoticeTypes              => Set<NoticeType>();
    public DbSet<NoticeTemplate>        NoticeTemplates          => Set<NoticeTemplate>();
    public DbSet<NoticeTemplateVersion> NoticeTemplateVersions   => Set<NoticeTemplateVersion>();
    public DbSet<NoticeDelivery>        NoticeDeliveries         => Set<NoticeDelivery>();
    public DbSet<PmTemplateOverride>    PmTemplateOverrides      => Set<PmTemplateOverride>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("notices");

        modelBuilder.Entity<NoticeType>(e =>
        {
            e.ToTable("notice_types");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.Code).HasColumnName("code").HasMaxLength(40).IsRequired();
            e.Property(x => x.DisplayName).HasColumnName("display_name").HasMaxLength(120).IsRequired();
            e.Property(x => x.LegalPriority).HasColumnName("legal_priority");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.HasIndex(x => x.Code).IsUnique();
        });

        modelBuilder.Entity<NoticeTemplate>(e =>
        {
            e.ToTable("notice_templates");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.StateId).HasColumnName("state_id");
            e.Property(x => x.NoticeTypeId).HasColumnName("notice_type_id");
            e.Property(x => x.IsActive).HasColumnName("is_active");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
        });

        modelBuilder.Entity<NoticeTemplateVersion>(e =>
        {
            e.ToTable("notice_template_versions");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.TemplateId).HasColumnName("template_id");
            e.Property(x => x.VersionNumber).HasColumnName("version_number");
            e.Property(x => x.Subject).HasColumnName("subject").HasMaxLength(400).IsRequired();
            e.Property(x => x.BodyHtml).HasColumnName("body_html").IsRequired();
            e.Property(x => x.BodyText).HasColumnName("body_text").IsRequired();
            e.Property(x => x.RequiredVars).HasColumnName("required_vars").IsRequired();
            e.Property(x => x.EffectiveDate).HasColumnName("effective_date");
            e.Property(x => x.ApprovedBy).HasColumnName("approved_by").HasMaxLength(200);
            e.Property(x => x.Status)
                .HasColumnName("status")
                .HasMaxLength(20)
                .HasConversion(v => v.ToWire(), v => NoticeEnumMapping.TemplateVersionStatusFromWire(v));
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
        });

        modelBuilder.Entity<NoticeDelivery>(e =>
        {
            e.ToTable("notice_deliveries");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.PropertyManagerId).HasColumnName("property_manager_id");
            e.Property(x => x.LeaseId).HasColumnName("lease_id");
            e.Property(x => x.TemplateVersionId).HasColumnName("template_version_id");
            e.Property(x => x.RenderedSubject).HasColumnName("rendered_subject").HasMaxLength(400).IsRequired();
            e.Property(x => x.RenderedBodyHtml).HasColumnName("rendered_body_html").IsRequired();
            e.Property(x => x.RenderedBodyText).HasColumnName("rendered_body_text").IsRequired();
            e.Property(x => x.Channel)
                .HasColumnName("channel")
                .HasMaxLength(20)
                .HasConversion(v => v.ToWire(), v => NoticeEnumMapping.ChannelFromWire(v));
            e.Property(x => x.SentAt).HasColumnName("sent_at");
            e.Property(x => x.ProviderMsgId).HasColumnName("provider_msg_id").HasMaxLength(200);
            e.Property(x => x.Status)
                .HasColumnName("status")
                .HasMaxLength(20)
                .HasConversion(v => v.ToWire(), v => NoticeEnumMapping.DeliveryStatusFromWire(v));
            e.Property(x => x.DeliveredAt).HasColumnName("delivered_at");
            e.Property(x => x.OpenedAt).HasColumnName("opened_at");
            e.Property(x => x.BouncedAt).HasColumnName("bounced_at");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.PmTemplateOverrideId).HasColumnName("pm_template_override_id");
        });

        modelBuilder.Entity<PmTemplateOverride>(e =>
        {
            e.ToTable("pm_template_overrides");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.PropertyManagerId).HasColumnName("property_manager_id");
            e.Property(x => x.NoticeTypeId).HasColumnName("notice_type_id");
            e.Property(x => x.StateId).HasColumnName("state_id");
            e.Property(x => x.Subject).HasColumnName("subject").HasMaxLength(400).IsRequired();
            e.Property(x => x.BodyHtml).HasColumnName("body_html").IsRequired();
            e.Property(x => x.BodyText).HasColumnName("body_text").IsRequired();
            e.Property(x => x.IsActive).HasColumnName("is_active");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
        });
    }
}
