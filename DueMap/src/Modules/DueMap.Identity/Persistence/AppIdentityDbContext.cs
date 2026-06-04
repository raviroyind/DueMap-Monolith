using DueMap.Identity.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace DueMap.Identity.Persistence;

/// <summary>
/// EF Core context over the <c>identity</c> schema. Inherits the standard
/// Identity table model and rebinds it to snake_case column names so the
/// schema matches the rest of the database (see db/duemap_schema_v7_identity.sql).
/// </summary>
public sealed class AppIdentityDbContext
    : IdentityDbContext<ApplicationUser, ApplicationRole, int>
{
    public AppIdentityDbContext(DbContextOptions<AppIdentityDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.HasDefaultSchema("identity");

        builder.Entity<ApplicationUser>(e =>
        {
            e.ToTable("users");
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.PropertyManagerId).HasColumnName("property_manager_id");
            e.Property(x => x.UserName).HasColumnName("user_name");
            e.Property(x => x.NormalizedUserName).HasColumnName("normalized_user_name");
            e.Property(x => x.Email).HasColumnName("email");
            e.Property(x => x.NormalizedEmail).HasColumnName("normalized_email");
            e.Property(x => x.EmailConfirmed).HasColumnName("email_confirmed");
            e.Property(x => x.PasswordHash).HasColumnName("password_hash");
            e.Property(x => x.SecurityStamp).HasColumnName("security_stamp");
            e.Property(x => x.ConcurrencyStamp).HasColumnName("concurrency_stamp");
            e.Property(x => x.PhoneNumber).HasColumnName("phone_number");
            e.Property(x => x.PhoneNumberConfirmed).HasColumnName("phone_number_confirmed");
            e.Property(x => x.TwoFactorEnabled).HasColumnName("two_factor_enabled");
            e.Property(x => x.LockoutEnd).HasColumnName("lockout_end");
            e.Property(x => x.LockoutEnabled).HasColumnName("lockout_enabled");
            e.Property(x => x.AccessFailedCount).HasColumnName("access_failed_count");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
        });

        builder.Entity<ApplicationRole>(e =>
        {
            e.ToTable("roles");
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.Name).HasColumnName("name");
            e.Property(x => x.NormalizedName).HasColumnName("normalized_name");
            e.Property(x => x.ConcurrencyStamp).HasColumnName("concurrency_stamp");
        });

        builder.Entity<IdentityUserRole<int>>(e =>
        {
            e.ToTable("user_roles");
            e.Property(x => x.UserId).HasColumnName("user_id");
            e.Property(x => x.RoleId).HasColumnName("role_id");
        });

        builder.Entity<IdentityRoleClaim<int>>(e =>
        {
            e.ToTable("role_claims");
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.RoleId).HasColumnName("role_id");
            e.Property(x => x.ClaimType).HasColumnName("claim_type");
            e.Property(x => x.ClaimValue).HasColumnName("claim_value");
        });

        builder.Entity<IdentityUserClaim<int>>(e =>
        {
            e.ToTable("user_claims");
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.UserId).HasColumnName("user_id");
            e.Property(x => x.ClaimType).HasColumnName("claim_type");
            e.Property(x => x.ClaimValue).HasColumnName("claim_value");
        });

        builder.Entity<IdentityUserLogin<int>>(e =>
        {
            e.ToTable("user_logins");
            e.Property(x => x.LoginProvider).HasColumnName("login_provider");
            e.Property(x => x.ProviderKey).HasColumnName("provider_key");
            e.Property(x => x.ProviderDisplayName).HasColumnName("provider_display_name");
            e.Property(x => x.UserId).HasColumnName("user_id");
        });

        builder.Entity<IdentityUserToken<int>>(e =>
        {
            e.ToTable("user_tokens");
            e.Property(x => x.UserId).HasColumnName("user_id");
            e.Property(x => x.LoginProvider).HasColumnName("login_provider");
            e.Property(x => x.Name).HasColumnName("name");
            e.Property(x => x.Value).HasColumnName("value");
        });
    }
}
