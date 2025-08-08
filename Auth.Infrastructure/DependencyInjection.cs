using Auth.Application.Interfaces;
using Auth.Application.UseCases.Telegram.Utils;
using Auth.Domain.Entities;
using Auth.Infrastructure.Data;
using Auth.Infrastructure.Repositories;
using Auth.Infrastructure.Services;
using Auth.Infrastructure.Telegram;
using Auth.Shared.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Auth.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config/*, IHostEnvironment env*/)
    {
        services.Configure<TelegramAuthOptions>(
        config.GetSection("Telegram"));

        services.AddSingleton(resolver =>
            resolver.GetRequiredService<IOptions<TelegramAuthOptions>>().Value);

        services.AddSingleton<ITelegramAuthVerifier, TelegramAuthVerifier>();
        services.AddSingleton<ITelegramPayloadValidator, TelegramPayloadValidator>();
        // DbContext
        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(config.GetConnectionString("DefaultConnection")));

        // Identity
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
        .AddEntityFrameworkStores<AppDbContext>()
        .AddDefaultTokenProviders();

        services.AddAuthorizationBuilder()
          // токен со скоупом "api" (универсальный доступ)
          .AddPolicy("Api", p => p.RequireClaim("scope", ApiScopes.Api))
          // чтение: подойдёт "api" ИЛИ "api:read"
          .AddPolicy("ApiRead", p => p.RequireClaim("scope", ApiScopes.Api, ApiScopes.ApiRead))
          // запись: требуется "api:write" (или, если хочешь, добавь сюда "api")
          .AddPolicy("ApiWrite", p => p.RequireClaim("scope", ApiScopes.ApiWrite));


        // Duende IdentityServer
        services.AddIdentityServer(options =>
        {
            options.UserInteraction.LoginUrl = "/Account/Login";
            options.UserInteraction.LogoutUrl = "/Account/Logout";
            options.UserInteraction.LoginReturnUrlParameter = "ReturnUrl";
            options.UserInteraction.LogoutIdParameter = "logoutId";

            options.Caching.ClientStoreExpiration = TimeSpan.FromMinutes(30);
            options.Authentication.CookieSlidingExpiration = true;
            options.Authentication.CoordinateClientLifetimesWithUserSession = true;
            options.Authentication.CookieSameSiteMode = SameSiteMode.None;
        })
        .AddAspNetIdentity<UserEntity>()
        .AddDeveloperSigningCredential()
        .AddConfigurationStore(opt =>
        {
            opt.ConfigureDbContext = b =>
                b.UseNpgsql(
                    config.GetConnectionString("DefaultConnection"),
                    sql => sql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName));
        })
        .AddOperationalStore(opt =>
        {
            opt.ConfigureDbContext = b =>
                b.UseNpgsql(
                    config.GetConnectionString("DefaultConnection"),
                    sql => sql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName));

            opt.EnableTokenCleanup = true;
            opt.TokenCleanupInterval = 3600;
        })
        // вернуть твои кастомы, если они используются:
        .AddProfileService<ProfileService>()
        .AddExtensionGrantValidator<TelegramGrantValidator>()
        // опционально:
        .AddServerSideSessions();

        // Репозитории и сервисы
        services.AddScoped<ITelegramRepository, TelegramRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ITelegramAuthVerifier, TelegramAuthVerifier>();

        return services;
    }
}
