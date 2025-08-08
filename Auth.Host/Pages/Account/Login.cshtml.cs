using System.ComponentModel.DataAnnotations;
using Auth.Domain.Entities;
using Duende.IdentityServer.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Auth.Host.Pages.Account;

public class LoginModel : PageModel
{
    private readonly SignInManager<UserEntity> _signInManager;
    private readonly IIdentityServerInteractionService _interaction;

    public LoginModel(SignInManager<UserEntity> signInManager,
                      IIdentityServerInteractionService interaction)
    {
        _signInManager = signInManager;
        _interaction = interaction;
    }

    [BindProperty]
    [Required(ErrorMessage = "Введите имя пользователя")]
    public string UserName { get; set; } = string.Empty;

    [BindProperty]
    [Required(ErrorMessage = "Введите пароль")]
    public string Password { get; set; } = string.Empty;

    [BindProperty]
    public bool RememberMe { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public void OnGet()
    {
        // Ничего: ReturnUrl уже будет привязан из query (?returnUrl=...)
    }

    [ValidateAntiForgeryToken]
    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
            return Page();

        var result = await _signInManager.PasswordSignInAsync(
            UserName, Password, RememberMe, lockoutOnFailure: true);

        if (result.Succeeded)
        {
            // Безопасно: редиректим только на валидный URL от IdentityServer
            if (!string.IsNullOrWhiteSpace(ReturnUrl) &&
                _interaction.IsValidReturnUrl(ReturnUrl))
            {
                return LocalRedirect(ReturnUrl!);
            }

            // Fallback: на корень, если returnUrl невалиден/отсутствует
            return LocalRedirect("/");
        }

        if (result.RequiresTwoFactor)
        {
            // если позже добавишь 2FA — сюда редирект на /Account/Login2fa?ReturnUrl=... etc.
            ModelState.AddModelError(string.Empty, "Требуется двухфакторная аутентификация.");
            return Page();
        }

        if (result.IsLockedOut)
        {
            ModelState.AddModelError(string.Empty, "Аккаунт временно заблокирован из-за неудачных попыток.");
            return Page();
        }

        if (result.IsNotAllowed)
        {
            ModelState.AddModelError(string.Empty, "Вход запрещён. Проверь подтверждение email/телефона или статус аккаунта.");
            return Page();
        }

        ModelState.AddModelError(string.Empty, "Неверное имя пользователя или пароль");
        return Page();
    }
}
