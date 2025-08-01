// Pages/Account/Login.cshtml.cs

using System.ComponentModel.DataAnnotations;
using ComputerAdminAuth.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ComputerAdminAuth.Pages.Account;

public class LoginModel : PageModel
{
    private readonly SignInManager<UserEntity> _signInManager;

    public LoginModel(SignInManager<UserEntity> signInManager)
    {
        _signInManager = signInManager;
    }

    [BindProperty]
    [Required(ErrorMessage = "Введите имя пользователя")]
    public string UserName { get; set; }

    [BindProperty]
    [Required(ErrorMessage = "Введите пароль")]
    public string Password { get; set; }

    [BindProperty]
    public bool RememberMe { get; set; }

    [BindProperty(SupportsGet = true)]
    public string ReturnUrl { get; set; }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var result = await _signInManager.PasswordSignInAsync(UserName, Password, RememberMe, lockoutOnFailure: true);

        if (result.Succeeded)
            return LocalRedirect(ReturnUrl ?? "/");

        ModelState.AddModelError(string.Empty, "Неверное имя пользователя или пароль");

        return Page();
    }
}
