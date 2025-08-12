using Auth.Application.Interfaces;
using Auth.Application.UseCases.Telegram.Utils;
using Auth.Domain.Entities;
using Auth.Infrastructure.Data;
using Auth.Infrastructure.Repositories;
using Auth.Infrastructure.Telegram;
using Auth.Shared.Contracts;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenIddict.Abstractions;
using OpenIddict.Validation.AspNetCore;

namespace Auth.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services.Configure<TelegramAuthOptions>(config.GetSection("Telegram"));
        services.AddSingleton(resolver => resolver.GetRequiredService<IOptions<TelegramAuthOptions>>().Value);

        services.AddSingleton<ITelegramAuthVerifier, TelegramAuthVerifier>();
        services.AddSingleton<ITelegramPayloadValidator, TelegramPayloadValidator>();

        // DbContext
        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(config.GetConnectionString("DefaultConnection")));

        // ASP.NET Identity
        services.AddIdentity<UserEntity, IdentityRole<Guid>>(options =>
        {
            options.Password.RequireDigit = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireUppercase = true;
            options.Password.RequiredLength = 8;
            options.Password.RequireNonAlphanumeric = false;
            options.SignIn.RequireConfirmedEmail = false;
            options.SignIn.RequireConfirmedPhoneNumber = false;
            options.User.RequireUniqueEmail = false;

            options.ClaimsIdentity.UserIdClaimType = System.Security.Claims.ClaimTypes.NameIdentifier;
            options.ClaimsIdentity.UserNameClaimType = System.Security.Claims.ClaimTypes.Name;
            options.ClaimsIdentity.RoleClaimType = System.Security.Claims.ClaimTypes.Role;
        })
        .AddEntityFrameworkStores<AppDbContext>()
        .AddDefaultTokenProviders();

        // Настройка cookie для Identity
        services.ConfigureApplicationCookie(options =>
        {
            options.LoginPath = "/Account/Login";
            options.LogoutPath = "/Account/Logout";
            options.Cookie.Name = "AuthCookie";
            options.Cookie.HttpOnly = true;
            options.ExpireTimeSpan = TimeSpan.FromDays(30);
            options.SlidingExpiration = true;
        });

        // Авторизационные политики
        services.AddAuthorizationBuilder()
          .AddPolicy("Api", p => p.RequireAssertion(ctx => ctx.User.HasScope(ApiScopes.Api)))
          .AddPolicy("ApiRead", p => p.RequireAssertion(ctx =>
               ctx.User.HasScope(ApiScopes.Api) || ctx.User.HasScope(ApiScopes.ApiRead)))
          .AddPolicy("ApiWrite", p => p.RequireAssertion(ctx =>
               ctx.User.HasScope(ApiScopes.ApiWrite) || ctx.User.HasScope(ApiScopes.Api)));

        // OpenIddict
        services.AddOpenIddict()
            .AddCore(opt =>
            {
                opt.UseEntityFrameworkCore()
                   .UseDbContext<AppDbContext>();
            })
            .AddServer(opt =>
            {
                opt.SetIssuer("https://auth.ava-kk.ru");

                opt.SetAuthorizationEndpointUris("/connect/authorize")
                   .SetTokenEndpointUris("/connect/token")
                   .SetUserInfoEndpointUris("/connect/userinfo")
                   .SetIntrospectionEndpointUris("/connect/introspect")
                   .SetRevocationEndpointUris("/connect/revocation")
                   .SetEndSessionEndpointUris("/connect/logout");

                opt.AllowAuthorizationCodeFlow()
                   .RequireProofKeyForCodeExchange()
                   .AllowRefreshTokenFlow();

                opt.RegisterScopes("openid", "profile",
                    ApiScopes.Api, ApiScopes.ApiRead, ApiScopes.ApiWrite, "offline_access");

                opt.UseAspNetCore()
                    .EnableAuthorizationEndpointPassthrough()
                    .EnableTokenEndpointPassthrough()
                    .EnableUserInfoEndpointPassthrough()
                    .EnableEndSessionEndpointPassthrough()
                    .EnableStatusCodePagesIntegration();

                opt.AddDevelopmentEncryptionCertificate()
                   .AddDevelopmentSigningCertificate();

                opt.DisableAccessTokenEncryption();
            })
            .AddValidation(opt =>
            {
                opt.UseLocalServer();
                opt.UseAspNetCore();

                opt.AddAudiences("computerclub_api");
            });

        // Репозитории и сервисы
        services.AddScoped<ITelegramRepository, TelegramRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddTransient<CustomSignInManager>();

        return services;
    }
}