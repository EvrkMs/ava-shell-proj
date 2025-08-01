using Duende.IdentityServer.EntityFramework.DbContexts;
using Duende.IdentityServer.EntityFramework.Mappers;
using Duende.IdentityServer.Models;
using Microsoft.EntityFrameworkCore;

namespace ComputerAdminAuth.Seeders;

public static class IdentityServerSeeder
{
    public static async Task SeedAsync(IServiceProvider serviceProvider)
    {
        var configDb = serviceProvider.GetRequiredService<ConfigurationDbContext>();

        if (!configDb.IdentityResources.Any())
        {
            var resources = new IdentityResource[]
            {
                new IdentityResources.OpenId(),
                new IdentityResources.Profile()
            };

            foreach (var res in resources)
                await configDb.IdentityResources.AddAsync(res.ToEntity());
        }

        if (!configDb.ApiScopes.Any())
        {
            var scopes = new ApiScope[]
            {
                new("api", "Full access to Computer Club API"),
                new("api:read", "Read-only access"),
                new("api:write", "Write access")
            };

            foreach (var scopeItem in scopes)
                await configDb.ApiScopes.AddAsync(scopeItem.ToEntity());
        }

        // по желанию
        if (!configDb.ApiResources.Any())
        {
            var apiResource = new ApiResource("computerclub_api", "Computer Club API")
            {
                Scopes = { "api", "api:read", "api:write" }
            };

            await configDb.ApiResources.AddAsync(apiResource.ToEntity());
        }

        await configDb.SaveChangesAsync();
    }
    public static async Task SeedClientsAsync(IServiceProvider provider)
    {
        var context = provider.GetRequiredService<ConfigurationDbContext>();

        if (!await context.Clients.AnyAsync(c => c.ClientId == "react-native-app"))
        {
            var client = new Client
            {
                ClientId = "react-native-app",
                AllowedGrantTypes = GrantTypes.Code,
                RequirePkce = true,
                RequireClientSecret = false,
                RedirectUris = { "computerclub-auth-tester://oauth/callback" },
                PostLogoutRedirectUris = { "computerclub-auth-tester://oauth/logout-callback" },
                AllowedCorsOrigins = { "http://localhost", "https://dns.ava-kk.com" },
                AllowedScopes = { "openid", "profile", "api" },
                AllowOfflineAccess = true,
                AccessTokenLifetime = 3600,
                RefreshTokenUsage = TokenUsage.OneTimeOnly,
                RefreshTokenExpiration = TokenExpiration.Absolute,
                SlidingRefreshTokenLifetime = 2592000
            };

            var entity = client.ToEntity();
            await context.Clients.AddAsync(entity);
            await context.SaveChangesAsync();
        }
    }
}
