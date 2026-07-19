using DueMap.Common.Modularity;
using DueMap.Identity.Domain;
using DueMap.Identity.Persistence;
using DueMap.Identity.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DueMap.Identity;

public sealed class IdentityModule : IModule
{
    public string Name => "Identity";

    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<IdentityModuleOptions>()
            .Bind(configuration.GetSection(IdentityModuleOptions.SectionName));

        var section = configuration.GetSection(IdentityModuleOptions.SectionName);
        var opts = section.Get<IdentityModuleOptions>() ?? new IdentityModuleOptions();

        var connectionString = section["ConnectionString"]
            ?? configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException(
                "Identity module: no connection string. Set ConnectionStrings:Default or Identity:ConnectionString.");

        services.AddDbContext<AppIdentityDbContext>(o =>
            o.UseSqlServer(connectionString,
                sql => sql.MigrationsHistoryTable("__identity_migrations", "identity")));

        services
            .AddIdentity<ApplicationUser, ApplicationRole>(o =>
            {
                o.Password.RequiredLength         = opts.MinPasswordLength;
                o.Password.RequireDigit           = opts.RequireDigit;
                o.Password.RequireUppercase       = opts.RequireUppercase;
                o.Password.RequireLowercase       = true;
                o.Password.RequireNonAlphanumeric = opts.RequireNonAlphanumeric;
                o.User.RequireUniqueEmail         = true;
                // Email/password sign-ups must confirm their address before they
                // can sign in. SSO (Intuit/Xero) and the demo seeder create users
                // with EmailConfirmed=true, so they're unaffected by this gate.
                o.SignIn.RequireConfirmedAccount  = true;

                // Password-reset tokens get their own provider so they can
                // expire in an hour without dragging the 24-hour email
                // confirmation window down with them.
                o.Tokens.PasswordResetTokenProvider = PasswordResetTokenProvider<ApplicationUser>.ProviderName;
            })
            .AddEntityFrameworkStores<AppIdentityDbContext>()
            .AddDefaultTokenProviders()
            .AddTokenProvider<PasswordResetTokenProvider<ApplicationUser>>(
                PasswordResetTokenProvider<ApplicationUser>.ProviderName);

        // Replace the default ClaimsFactory so every sign-in carries the PmId claim.
        services.AddScoped<IUserClaimsPrincipalFactory<ApplicationUser>, PmClaimsFactory>();

        // Cross-context shim — Worker jobs (Daily Close Report) need to find a
        // PM's primary contact email; that lives on AspNetUsers, not in Tenancy.
        services.AddScoped<IUserDirectory, Services.UserDirectory>();

        services.ConfigureApplicationCookie(o =>
        {
            o.LoginPath          = "/Account/Login";
            o.LogoutPath         = "/Account/Logout";
            o.AccessDeniedPath   = "/Account/AccessDenied";
            o.ExpireTimeSpan     = TimeSpan.FromDays(14);
            o.SlidingExpiration  = true;
            o.Cookie.Name        = "DueMap.Auth";
            o.Cookie.SameSite    = Microsoft.AspNetCore.Http.SameSiteMode.Lax;
            o.Cookie.HttpOnly    = true;
        });
    }
}
