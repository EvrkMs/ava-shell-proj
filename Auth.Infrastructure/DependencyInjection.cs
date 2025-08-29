using Auth.Application.Interfaces;
using Auth.Domain.Entities;
using Auth.EntityFramework.Data;
using Auth.EntityFramework.Repositories;
using Auth.Infrastructure.Data;
using Auth.Shared.Contracts;
using Auth.TelegramAuth.Interface;
using Auth.TelegramAuth.Options;
using Auth.TelegramAuth.Service;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Cryptography.X509Certificates;
using OpenIddict.Abstractions;

namespace Auth.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        // 1) Настраиваем Options из секции
        services.Configure<TelegramAuthOptions>(config.GetSection("Telegram"));

        // 2) Регистрируем сервис, который возьмёт IOptions<TelegramAuthOptions>
        services.AddSingleton<ITelegramAuthService>(sp =>
        {
            var opt = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<TelegramAuthOptions>>().Value;
            return new TelegramAuthService(opt);
        });

        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // === DbContext ===
        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(config.GetConnectionString("DefaultConnection")));

        // === ASP.NET Identity ===
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

        services.ConfigureApplicationCookie(options =>
        {
            options.LoginPath = "/Account/Login";
            options.LogoutPath = "/Account/Logout";
            options.Cookie.Name = "AuthCookie";
            options.Cookie.HttpOnly = true;
            options.ExpireTimeSpan = TimeSpan.FromDays(30);
            options.SlidingExpiration = true;
        });

        // === Политики ===
        services.AddAuthorizationBuilder()
          .AddPolicy("Api", p => p.RequireAssertion(ctx => ctx.User.HasScope(ApiScopes.Api)))
          .AddPolicy("ApiRead", p => p.RequireAssertion(ctx =>
               ctx.User.HasScope(ApiScopes.Api) || ctx.User.HasScope(ApiScopes.ApiRead)))
          .AddPolicy("ApiWrite", p => p.RequireAssertion(ctx =>
               ctx.User.HasScope(ApiScopes.ApiWrite) || ctx.User.HasScope(ApiScopes.Api)));
        // === OpenIddict ===
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

                opt.AllowClientCredentialsFlow();

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

                // Try to load signing certificate from env/config for production
                var signingPath = config["OpenIddict:SigningCertificate:Path"] ?? Environment.GetEnvironmentVariable("OIDC_SIGNING_CERTIFICATE_PATH");
                var signingPwd = config["OpenIddict:SigningCertificate:Password"] ?? Environment.GetEnvironmentVariable("OIDC_SIGNING_CERTIFICATE_PASSWORD");
                if (!string.IsNullOrWhiteSpace(signingPath) && File.Exists(signingPath))
                {
                    var cert = X509CertificateLoader.LoadPkcs12FromFile(signingPath, signingPwd, X509KeyStorageFlags.MachineKeySet);
                    opt.AddSigningCertificate(cert);
                }
                else
                {
                    opt.AddDevelopmentSigningCertificate();
                }

                // Encryption certificate is optional when access token encryption is disabled
                opt.AddDevelopmentEncryptionCertificate();

                opt.DisableAccessTokenEncryption();
            })
            .AddValidation(opt =>
            {
                opt.UseLocalServer();
                opt.UseAspNetCore();
                opt.AddAudiences("computerclub_api");
            });

        // === Репозитории и сервисы ===
        services.AddScoped<ITelegramRepository, TelegramRepository>();
        services.AddScoped<IUserRepository, UserRepository>();

        services.AddTransient<CustomSignInManager>();

        return services;
    }
}
