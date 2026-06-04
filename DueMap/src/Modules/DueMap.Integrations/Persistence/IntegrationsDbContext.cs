using DueMap.Integrations.Accounting;
using Microsoft.EntityFrameworkCore;

namespace DueMap.Integrations.Persistence;

public sealed class IntegrationsDbContext : DbContext
{
    public IntegrationsDbContext(DbContextOptions<IntegrationsDbContext> options) : base(options) { }

    public DbSet<PmAccountingConnection> PmAccountingConnections => Set<PmAccountingConnection>();
    public DbSet<OAuthAttempt>           OAuthAttempts           => Set<OAuthAttempt>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("integrations");

        modelBuilder.Entity<PmAccountingConnection>(e =>
        {
            e.ToTable("pm_accounting_connections");
            e.HasKey(x => x.PropertyManagerId);
            e.Property(x => x.PropertyManagerId).HasColumnName("property_manager_id").ValueGeneratedNever();
            e.Property(x => x.Provider)
                .HasColumnName("provider")
                .HasMaxLength(20)
                .HasConversion(v => v.ToWire(), v => AccountingProviderMapping.FromWire(v));
            e.Property(x => x.RealmId).HasColumnName("realm_id").HasMaxLength(200).IsRequired();
            e.Property(x => x.AccessTokenProtected).HasColumnName("access_token_protected").IsRequired();
            e.Property(x => x.RefreshTokenProtected).HasColumnName("refresh_token_protected").IsRequired();
            e.Property(x => x.AccessTokenExpiresAt).HasColumnName("access_token_expires_at");
            e.Property(x => x.RefreshTokenExpiresAt).HasColumnName("refresh_token_expires_at");
            e.Property(x => x.Scopes).HasColumnName("scopes").HasMaxLength(500);
            e.Property(x => x.ConnectedAt).HasColumnName("connected_at");
            e.Property(x => x.LastSyncAt).HasColumnName("last_sync_at");
            e.Property(x => x.LastSyncError).HasColumnName("last_sync_error");
            e.Property(x => x.Status)
                .HasColumnName("status")
                .HasMaxLength(20)
                .HasConversion(v => v.ToWire(), v => ConnectionStatusMapping.FromWire(v));
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        });

        modelBuilder.Entity<OAuthAttempt>(e =>
        {
            e.ToTable("oauth_attempts");
            e.HasKey(x => x.State);
            e.Property(x => x.State).HasColumnName("state").HasMaxLength(100).ValueGeneratedNever();
            e.Property(x => x.PropertyManagerId).HasColumnName("property_manager_id");
            e.Property(x => x.Provider)
                .HasColumnName("provider")
                .HasMaxLength(20)
                .HasConversion(v => v.ToWire(), v => AccountingProviderMapping.FromWire(v));
            e.Property(x => x.RedirectUri).HasColumnName("redirect_uri").HasMaxLength(500).IsRequired();
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.ConsumedAt).HasColumnName("consumed_at");
        });
    }
}
