using Auth.Application.UseCases.Telegram;
using Auth.Domain.Entities;
using Auth.TelegramAuth.Raw;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Auth.Host.Pages.Account.Telegram;

[Authorize]
public class TelegramBindModel : PageModel
{
    private readonly BindTelegramCommand _bindTelegram;
    private readonly UserManager<UserEntity> _userManager;

    public TelegramBindModel(
        BindTelegramCommand bindTelegram,
        UserManager<UserEntity> userManager)
    {
        _bindTelegram = bindTelegram;
        _userManager = userManager;
    }

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Error { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Message { get; set; }

    public void OnGet() { }

    // GET от Telegram Login Widget
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> OnGetVerifyAsync(
        string id, string first_name, string last_name, string username,
        string photo_url, long auth_date, string hash, string? returnUrl, CancellationToken ct)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null)
        {
            return RedirectToPage("/Account/Login", new
            {
                returnUrl = Url.Page("/Account/Telegram/TelegramBind", new { returnUrl = returnUrl ?? "/" }),
                error = "not_authenticated"
            });
        }

        if (!currentUser.IsActive)
        {
            return RedirectToPage("/Account/Telegram/TelegramBind", new
            {
                returnUrl = returnUrl,
                error = "Учетная запись неактивна"
            });
        }

        var dto = new TelegramRawData()
        {
            Id = long.Parse(id),
            Username = username,
            FirstName = first_name,
            LastName = last_name,
            PhotoUrl = photo_url,
            AuthDate = auth_date,
            Hash = hash
        };

        // Новая команда уже сама вызывает ITelegramAuthService → BotToken не нужен
        var result = await _bindTelegram.ExecuteAsync(currentUser.Id, dto, ct);

        if (result.Success)
        {
            return RedirectToPage("/Account/Telegram/TelegramBind", new
            {
                returnUrl = returnUrl,
                message = "Telegram успешно привязан!"
            });
        }

        return RedirectToPage("/Account/Telegram/TelegramBind", new
        {
            returnUrl = returnUrl,
            error = result.Error ?? "Не удалось привязать Telegram"
        });
    }
}
