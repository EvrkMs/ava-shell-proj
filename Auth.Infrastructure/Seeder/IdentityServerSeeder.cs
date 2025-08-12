using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Auth.Infrastructure.Seeder
{
    public static class OpenIddictSeeder
    {
        public static async Task SeedAsync(IServiceProvider sp)
        {
            using var scope = sp.CreateScope();
            var scopeMgr = scope.ServiceProvider.GetRequiredService<IOpenIddictScopeManager>();
            var appMgr = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();

            // --- Scopes ---
            await EnsureScopeAsync(scopeMgr, "openid", "OpenID Connect");              // без ресурсов
            await EnsureScopeAsync(scopeMgr, "profile", "Profile");                      // без ресурсов
            await EnsureScopeAsync(scopeMgr, "telegram", "Telegram profile");             // без ресурсов

            await EnsureScopeAsync(scopeMgr, "api", "Full access to Computer Club API", ["computerclub_api"]);
            await EnsureScopeAsync(scopeMgr, "api:read", "Read-only access", ["computerclub_api"]);
            await EnsureScopeAsync(scopeMgr, "api:write", "Write access", ["computerclub_api"]);

            // --- Application (react-spa) ---
            var clientId = "react-spa";
            var existing = await appMgr.FindByClientIdAsync(clientId);

            var desc = new OpenIddictApplicationDescriptor
            {
                ClientId = clientId,
                DisplayName = "React SPA",
                ClientType = ClientTypes.Public,
                ConsentType = ConsentTypes.Explicit
            };

            desc.RedirectUris.Add(new Uri("https://admin.ava-kk.ru/callback"));
            desc.RedirectUris.Add(new Uri("https://admin.ava-kk.ru/silent-callback.html"));
            desc.PostLogoutRedirectUris.Add(new Uri("https://admin.ava-kk.ru/logout-callback"));

            desc.Permissions.UnionWith(new[]
            {
                // endpoints
                Permissions.Endpoints.Authorization,
                Permissions.Endpoints.Token,
                Permissions.Endpoints.Revocation,

                Permissions.Endpoints.EndSession,

                // grant/response
                Permissions.GrantTypes.AuthorizationCode,
                Permissions.ResponseTypes.Code,

                // scopes (варианты констант у OpenIddict разные: для openid — используем Scopes.OpenId)
                Scopes.OpenId,
                Permissions.Scopes.Profile,
                Permissions.Prefixes.Scope + "telegram",
                Permissions.Prefixes.Scope + "api",
                Permissions.Prefixes.Scope + "api:read",
                Permissions.Prefixes.Scope + "api:write",
                Permissions.Prefixes.Scope + "offline_access"
            });

            // PKCE для публичного клиента
            desc.Requirements.Add(Requirements.Features.ProofKeyForCodeExchange);

            if (existing is null)
                await appMgr.CreateAsync(desc);
            else
                await appMgr.UpdateAsync(existing, desc);
        }

        private static async Task EnsureScopeAsync(
            IOpenIddictScopeManager scopeMgr,
            string name,
            string? displayName = null,
            string[]? resources = null)
        {
            var handle = await scopeMgr.FindByNameAsync(name);
            if (handle is null)
            {
                var d = new OpenIddictScopeDescriptor
                {
                    Name = name,
                    DisplayName = displayName ?? name
                };
                if (resources is { Length: > 0 })
                    foreach (var r in resources) d.Resources.Add(r);

                await scopeMgr.CreateAsync(d);
                return;
            }

            // Обновление: укажи Name и полностью нужные поля (минимум Name + Resources)
            var update = new OpenIddictScopeDescriptor
            {
                Name = name,
                DisplayName = displayName ?? name
            };

            // перезапишем ресурсы ровно на те, что хотим
            update.Resources.UnionWith(resources ?? Array.Empty<string>());

            await scopeMgr.UpdateAsync(handle, update);
        }
    }
}
