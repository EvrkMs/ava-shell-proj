using Auth.Application.Interfaces;
using Auth.Domain.Entities;
using Auth.EntityFramework.Data;
using Auth.EntityFramework.Repositories;
using Auth.Infrastructure.Data;
using Auth.Infrastructure.Services;
using Auth.Shared.Contracts;
using Auth.TelegramAuth.Interface;
using Auth.TelegramAuth.Options;
using Auth.TelegramAuth.Service;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

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
        services.AddScoped<ISessionRepository, SessionRepository>();
        services.AddScoped<ISessionService, SessionService>();

        // === DbContext ===
        services.AddDbContext<AppDbContext>(options =>
        {
            var cs = config.GetConnectionString("DefaultConnection");

            // GitHub Secrets делает всё UPPERCASE, поэтому ищем вручную
            if (string.IsNullOrEmpty(cs))
                cs = config["CONNECTIONSTRINGS__DEFAULTCONNECTION"];

            if (string.IsNullOrEmpty(cs))
                throw new InvalidOperationException("Database connection string not found in configuration or environment.");

            options.UseNpgsql(cs, npgsql =>
            {
                // Resiliency against transient DB/network issues + sane timeouts
                npgsql.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(3),
                    errorCodesToAdd: null);
                npgsql.CommandTimeout(15);
            });
        });

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
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.ExpireTimeSpan = TimeSpan.FromDays(30);
            options.SlidingExpiration = true;
        });

        // Data Protection: persist keys to volume to keep cookies/tokens valid across restarts
        var dpKeysDir = config["DataProtection:KeysDirectory"] ?? "/keys/dataprotection";
        services.AddDataProtection()
            .PersistKeysToFileSystem(new DirectoryInfo(dpKeysDir))
            .SetApplicationName("auth-host")
            .SetDefaultKeyLifetime(TimeSpan.FromDays(90));

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

                // Load signing certificate or autogenerate persistent PFX in volume if missing
                var signingPath = config["OpenIddict:SigningCertificate:Path"]
                                   ?? Environment.GetEnvironmentVariable("OIDC_SIGNING_CERTIFICATE_PATH")
                                   ?? "/keys/openiddict/signing.pfx";
                var signingPwd = config["OpenIddict:SigningCertificate:Password"]
                                  ?? Environment.GetEnvironmentVariable("OIDC_SIGNING_CERTIFICATE_PASSWORD");

                try
                {
                    var dir = Path.GetDirectoryName(signingPath);
                    if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
                        Directory.CreateDirectory(dir);

                    if (!File.Exists(signingPath) && !string.IsNullOrWhiteSpace(signingPwd))
                    {
                        using var rsa = RSA.Create(2048);
                        var req = new CertificateRequest(
                            new X500DistinguishedName("CN=auth-openiddict"),
                            rsa,
                            HashAlgorithmName.SHA256,
                            RSASignaturePadding.Pkcs1);

                        req.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
                        req.CertificateExtensions.Add(new X509KeyUsageExtension(
                            X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, false));

                        var now = DateTimeOffset.UtcNow.AddMinutes(-5);
                        using var certGen = req.CreateSelfSigned(now, now.AddYears(5));
                        var pfxBytes = certGen.Export(X509ContentType.Pfx, signingPwd);
                        File.WriteAllBytes(signingPath, pfxBytes);
                    }

                    if (File.Exists(signingPath) && !string.IsNullOrWhiteSpace(signingPwd))
                    {
                        var cert = X509CertificateLoader.LoadPkcs12FromFile(signingPath, signingPwd, X509KeyStorageFlags.MachineKeySet);
                        opt.AddSigningCertificate(cert);
                    }
                    else
                    {
                        opt.AddDevelopmentSigningCertificate();
                    }
                }
                catch
                {
                    opt.AddDevelopmentSigningCertificate();
                }

                // Encryption certificate is optional when access token encryption is disabled
                opt.AddDevelopmentEncryptionCertificate();

                opt.DisableAccessTokenEncryption();

                // Token lifetimes (corporate-friendly: short access token, longer refresh)
                opt.SetAccessTokenLifetime(TimeSpan.FromMinutes(10));
                opt.SetRefreshTokenLifetime(TimeSpan.FromDays(30));

                // Use reference access tokens (server-state + supports revocation)
                opt.UseReferenceAccessTokens();
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
