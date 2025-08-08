using Auth.Shared.Contracts;
using Duende.IdentityModel;
using Duende.IdentityServer.Models;

namespace Auth.Host.Seeder;

internal static class IdentityServerSeeder
{
    public static IEnumerable<IdentityResource> GetIdentityResources() =>
    [
        new IdentityResources.OpenId(),
        new IdentityResources.Profile(),
        new IdentityResource(
            name: "telegram",
            displayName: "Telegram profile",
            userClaims:
            [
                CustomClaimTypesConst.TelegramId,
                CustomClaimTypesConst.TelegramUsername,
                CustomClaimTypesConst.TelegramLinked,
                JwtClaimTypes.Picture
            ])
    ];

    public static IEnumerable<ApiScope> GetApiScopes() =>
    [
        new ApiScope(ApiScopes.Api, "Full access to Computer Club API"),
        new ApiScope(ApiScopes.ApiRead, "Read-only access"),
        new ApiScope(ApiScopes.ApiWrite, "Write access")
    ];

    public static IEnumerable<ApiResource> GetApiResources() =>
    [
        new ApiResource(ApiResources.ComputerClubApi, "Computer Club API")
        {
            Scopes = { ApiScopes.Api, ApiScopes.ApiRead, ApiScopes.ApiWrite }
        }
    ];

    public static IEnumerable<Client> GetClients() =>
    [
        new Client
        {
            ClientId = "react-spa",
            AllowedGrantTypes =
            {
                GrantType.AuthorizationCode,
            },
            RequirePkce = true,
            RequireClientSecret = false,
            RedirectUris =
            {
                "https://admin.ava-kk.ru/callback",
                "https://admin.ava-kk.ru/silent-callback.html"
            },
            PostLogoutRedirectUris = { "https://admin.ava-kk.ru/logout-callback" },
            AllowedCorsOrigins = { "https://admin.ava-kk.ru" },

            AllowedScopes = { "openid", "profile", ApiScopes.Api },
            AllowOfflineAccess = true,
            AccessTokenLifetime = 3600,
            RefreshTokenUsage = TokenUsage.OneTimeOnly,
            RefreshTokenExpiration = TokenExpiration.Sliding,
            SlidingRefreshTokenLifetime = 2592000
        }
    ];
}
