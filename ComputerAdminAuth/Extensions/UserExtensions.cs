using ComputerAdminAuth.Data.Context;
using ComputerAdminAuth.Data.Extension;
using ComputerAdminAuth.Entities;
using ComputerAdminAuth.Service;
using Duende.IdentityServer.Models;
using Microsoft.AspNetCore.Identity;

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
        })
        .AddAspNetIdentity<UserEntity>()
        .AddInMemoryIdentityResources(GetIdentityResources())
        .AddInMemoryApiResources(GetApiResources())
        .AddInMemoryApiScopes(GetApiScopes())
        .AddInMemoryClients(GetClients())
        .AddProfileService<ProfileService>()
        .AddServerSideSessions();

        return services;
    }

    private static IEnumerable<IdentityResource> GetIdentityResources() =>
    [
        new IdentityResources.OpenId(),
        new IdentityResources.Profile()
    ];

    private static IEnumerable<ApiScope> GetApiScopes() =>
    [
        new ApiScope("api", "Full access to Computer Club API"),
        new ApiScope("api:read", "Read-only access"),
        new ApiScope("api:write", "Write access")
    ];

    private static IEnumerable<ApiResource> GetApiResources() =>
    [
        new ApiResource("computerclub_api", "Computer Club API")
        {
            Scopes = { "api", "api:read", "api:write" }
        }
    ];

    private static IEnumerable<Client> GetClients() =>
    [
        new Client
        {
            ClientId = "react-native-app",
            AllowedGrantTypes = GrantTypes.Code,
            RequirePkce = true,
            RequireClientSecret = false,

            RedirectUris = { "https://dns.ava-kk.com/admin/callback" },
            PostLogoutRedirectUris = { "https://dns.ava-kk.com/admin/logout-callback" },
            AllowedCorsOrigins = { "http://localhost", "https://dns.ava-kk.com" },

            AllowedScopes = { "openid", "profile", "api" },
            AllowOfflineAccess = true,
            AccessTokenLifetime = 3600,
            RefreshTokenUsage = TokenUsage.OneTimeOnly,
            RefreshTokenExpiration = TokenExpiration.Absolute,
            SlidingRefreshTokenLifetime = 2592000
        },
        new Client
        {
            ClientId = "telegram-introspection",
            ClientSecrets = { new Secret("super-secret".Sha256()) },
            AllowedGrantTypes = GrantTypes.ClientCredentials,
            AllowedScopes = { "api" }
        }
    ];
}