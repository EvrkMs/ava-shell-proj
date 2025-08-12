using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OpenIddict.Server.AspNetCore;

namespace Auth.Host.Pages.Account
{
    public class LogoutModel : PageModel
    {
        [BindProperty(SupportsGet = true)]
        public string? ReturnUrl { get; set; }

        public bool AutomaticRedirectAfterSignOut => true;

        public void OnGet()
        {
            // Получаем post_logout_redirect_uri из OIDC запроса
            var request = HttpContext.GetOpenIddictServerRequest();
            if (!string.IsNullOrEmpty(request?.PostLogoutRedirectUri))
            {
                ViewData["PostLogoutRedirectUri"] = request.PostLogoutRedirectUri;
            }
        }

        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OnPostAsync()
        {
            await SignOutAllAsync();

            // Проверяем, есть ли OIDC post_logout_redirect_uri
            var request = HttpContext.GetOpenIddictServerRequest();
            if (!string.IsNullOrEmpty(request?.PostLogoutRedirectUri))
            {
                return SignOut(
                    authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                    properties: new AuthenticationProperties
                    {
                        RedirectUri = request.PostLogoutRedirectUri
                    });
            }

            var target = SafeReturn(ReturnUrl);
            return AutomaticRedirectAfterSignOut
                ? LocalRedirect(target)
                : Page();
        }

        private async Task SignOutAllAsync()
        {
            await HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
            await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);
        }

        private string SafeReturn(string? url)
            => (!string.IsNullOrWhiteSpace(url) && Url.IsLocalUrl(url)) ? url! : "/";
    }
}