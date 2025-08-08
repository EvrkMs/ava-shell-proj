using Duende.IdentityServer;
using Duende.IdentityServer.Events;
using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Auth.Host.Pages.Account;

public class LogoutModel(
        IIdentityServerInteractionService interaction,
        IEventService events) : PageModel
{
    private readonly IIdentityServerInteractionService _interaction = interaction;
    private readonly IEventService _events = events;

    // данные для Razor‑View
    public string? PostLogoutRedirectUri { get; private set; }
    public string? ClientName { get; private set; }
    public string? SignOutIframeUrl { get; private set; }
    public bool AutomaticRedirectAfterSignOut => true;

    /* ---------- GET: показываем форму или сразу выполняем логаут ---------- */
    public async Task<IActionResult> OnGetAsync(string? logoutId)
    {
        // если IdentityServer настроен без подтверждения, создаём POST‑логаут сами
        logoutId ??= await _interaction.CreateLogoutContextAsync();

        var ctx = await _interaction.GetLogoutContextAsync(logoutId);

        // нет формы — сразу POST
        if (ctx?.ShowSignoutPrompt == false)
        {
            return await LogoutAsync(logoutId);
        }

        // иначе показываем страницу Logout.cshtml (кнопка «Выйти» → POST)
        PostLogoutRedirectUri = ctx?.PostLogoutRedirectUri;
        ClientName = ctx?.ClientName ?? ctx?.ClientId;
        SignOutIframeUrl = ctx?.SignOutIFrameUrl;
        ViewData["LogoutId"] = logoutId;

        return Page();
    }

    /* ---------- POST: собственно выход ---------- */
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> OnPostAsync(string logoutId)
        => await LogoutAsync(logoutId);

    /* ---------- общий метод ---------- */
    private async Task<IActionResult> LogoutAsync(string logoutId)
    {
        /* 1. удаляем куки всех схем, которые держат пользователя аутентифицированным */
        await HttpContext.SignOutAsync(IdentityServerConstants.DefaultCookieAuthenticationScheme);     // idsrv
        await HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);                          // ASP.NET Core Identity
        await HttpContext.SignOutAsync(IdentityServerConstants.ExternalCookieAuthenticationScheme);   // внешние провайдеры (если были)

        /* 2. событие для аудита */
        if (User?.Identity?.IsAuthenticated == true)
        {
            await _events.RaiseAsync(
                new UserLogoutSuccessEvent(User.GetSubjectId(), User.GetDisplayName()));
        }

        /* 3. получаем контекст, чтобы отдать iFrame + редирект */
        var ctx = await _interaction.GetLogoutContextAsync(logoutId);

        PostLogoutRedirectUri = ctx?.PostLogoutRedirectUri;
        ClientName = ctx?.ClientName ?? ctx?.ClientId;
        SignOutIframeUrl = ctx?.SignOutIFrameUrl;

        /* 4. Уведомляем клиентов через фронт‑канал (если нужно) */
        if (SignOutIframeUrl != null)
        {
            Response.Headers.Add("Logout-IFrame-Url", SignOutIframeUrl);
        }

        /* 5. Авто‑редирект или страница «Вы вышли» */
        return AutomaticRedirectAfterSignOut && PostLogoutRedirectUri != null
            ? Redirect(PostLogoutRedirectUri)
            : Page();
    }
}
