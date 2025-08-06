// Seeders/IdentityServerSeeder.cs
using ComputerAdminAuth.Extensions;
using ComputerAdminAuth.Services;
using Duende.IdentityModel;
using Duende.IdentityServer.EntityFramework.DbContexts;
using Duende.IdentityServer.EntityFramework.Mappers;
using Duende.IdentityServer.Models;

namespace ComputerAdminAuth.Seeders;

public static class IdentityServerSeeder
{
    public static async Task SeedAsync(IServiceProvider sp)
    {
        using var scope = sp.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ConfigurationDbContext>();

        if (!ctx.Clients.Any())
        {
            ctx.Clients.Add(
                GetClients()
                              .First()              // ← ваш react-spa
                              .ToEntity());
        }

        if (!ctx.IdentityResources.Any())
        {
            ctx.IdentityResources.AddRange(
                GetIdentityResources()  // static методы выше
                              .Select(r => r.ToEntity()));
        }

        if (!ctx.ApiScopes.Any())
        {
            ctx.ApiScopes.AddRange(
                GetApiScopes()
                              .Select(s => s.ToEntity()));
        }

        if (!ctx.ApiResources.Any())
        {
            ctx.ApiResources.AddRange(
                GetApiResources()
                              .Select(r => r.ToEntity()));
        }

        await ctx.SaveChangesAsync();
    }
    private static IEnumerable<IdentityResource> GetIdentityResources() =>
    [
        new IdentityResources.OpenId(),
        new IdentityResources.Profile(),
        new IdentityResource(
        name:     "telegram",
        displayName: "Telegram profile",
        userClaims:
        [
            CustomClaimTypes.TelegramId,
            CustomClaimTypes.TelegramUsername,
            CustomClaimTypes.TelegramLinked,
            JwtClaimTypes.Picture
        ])
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
            ClientId = "react-spa",
            AllowedGrantTypes =
            {
                GrantType.AuthorizationCode,
                "telegram_login"
            },
            RequirePkce = true,
            RequireClientSecret = false,

            RedirectUris = {
                "https://admin.ava-kk.ru/callback",
                "https://admin.ava-kk.ru/silent-callback.html"
            },
            PostLogoutRedirectUris = { "https://admin.ava-kk.ru/logout-callback" },
            AllowedCorsOrigins = { "https://admin.ava-kk.ru" },

            AllowedScopes = { "openid", "profile", "api" },
            AllowOfflineAccess = true,
            AccessTokenLifetime = 3600,
            RefreshTokenUsage = TokenUsage.OneTimeOnly,
            RefreshTokenExpiration = TokenExpiration.Absolute,
            SlidingRefreshTokenLifetime = 2592000
        }
    ];
}