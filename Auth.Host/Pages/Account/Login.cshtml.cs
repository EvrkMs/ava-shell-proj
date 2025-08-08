using System.ComponentModel.DataAnnotations;
using Auth.Infrastructure; // твой CustomSignInManager
using Duende.IdentityServer.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Auth.Host.Pages.Account;

[ValidateAntiForgeryToken] // окей, пусть висит на классе
public class LoginModel : PageModel
{
    private readonly CustomSignInManager _signInManager;
    private readonly IIdentityServerInteractionService _interaction;

    public LoginModel(CustomSignInManager signInManager,
                      IIdentityServerInteractionService interaction)
    {
        _signInManager = signInManager;
        _interaction = interaction;
    }

    [BindProperty, Required(ErrorMessage = "Введите имя пользователя")]
    public string UserName { get; set; } = string.Empty;

    [BindProperty, Required(ErrorMessage = "Введите пароль")]
    public string Password { get; set; } = string.Empty;

    [BindProperty]
    public bool RememberMe { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    [IgnoreAntiforgeryToken] // иначе GET требует токен
    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
            return Page();

        var result = await _signInManager.PasswordSignInAsync(
            UserName, Password, RememberMe, lockoutOnFailure: true);

        if (result.Succeeded)
        {
            if (!string.IsNullOrWhiteSpace(ReturnUrl) && _interaction.IsValidReturnUrl(ReturnUrl))
                return LocalRedirect(ReturnUrl);

            return LocalRedirect("/");
        }

        if (result.RequiresTwoFactor)
        {
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
            // Форсим смену пароля: уводим на анонимную страницу ChangePassword
            var safeReturn = (!string.IsNullOrWhiteSpace(ReturnUrl) && _interaction.IsValidReturnUrl(ReturnUrl))
                ? ReturnUrl : "/";

            return RedirectToPage("/Account/ChangePassword", new
            {
                userName = UserName,
                returnUrl = safeReturn,
                requireChange = true
            });
        }

        ModelState.AddModelError(string.Empty, "Неверное имя пользователя или пароль");
        return Page();
    }
}
