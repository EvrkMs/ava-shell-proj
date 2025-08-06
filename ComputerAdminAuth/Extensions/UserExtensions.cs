using ComputerAdminAuth.Data.Context;
using ComputerAdminAuth.Data.Extension;
using ComputerAdminAuth.Entities;
using ComputerAdminAuth.Identity;
using ComputerAdminAuth.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ComputerAdminAuth.Extensions;

public static class UserExtensions
{
    public static IServiceCollection AddUserServices(this IServiceCollection services, IConfiguration config)
    {
        services.AddDbConnection(config);

        services.AddIdentity<UserEntity, IdentityRole<Guid>>(options =>
        {
            options.Password.RequireDigit = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireUppercase = true;
            options.Password.RequiredLength = 8;
            options.Password.RequireNonAlphanumeric = false;
            options.SignIn.RequireConfirmedEmail = false;
            options.User.RequireUniqueEmail = false;
            options.SignIn.RequireConfirmedPhoneNumber = false;
        })
        .AddRoles<IdentityRole<Guid>>()
        .AddEntityFrameworkStores<AppDbContext>()
        .AddDefaultTokenProviders();

        services.AddIdentityServer(options =>
        {
            options.Caching.ClientStoreExpiration = TimeSpan.FromMinutes(30);
            options.Authentication.CookieSlidingExpiration = true;
            options.Authentication.CoordinateClientLifetimesWithUserSession = true;
            options.Authentication.CookieSameSiteMode = SameSiteMode.None;
        })
        .AddConfigurationStore(opt =>
        {
            opt.ConfigureDbContext = b =>
                b.UseNpgsql(config.GetConnectionString("DefaultConnection"),
                            sql => sql.MigrationsAssembly(typeof(UserExtensions).Assembly.FullName));
        })
        .AddOperationalStore(opt =>
        {
            opt.ConfigureDbContext = b =>
                b.UseNpgsql(config.GetConnectionString("DefaultConnection"),
                            sql => sql.MigrationsAssembly(typeof(UserExtensions).Assembly.FullName));

            // автоматическая очистка токенов/код-авт. по расписанию
            opt.EnableTokenCleanup = true;
            opt.TokenCleanupInterval = 3600;   // раз в час
        })
        .AddAspNetIdentity<UserEntity>()
        .AddExtensionGrantValidator<TelegramGrantValidator>()
        .AddProfileService<ProfileService>()
        .AddServerSideSessions();

        return services;
    }
}