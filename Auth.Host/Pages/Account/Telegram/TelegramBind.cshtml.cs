using System.Security.Claims;
using System.Text.Encodings.Web;
using Auth.Application.UseCases.Telegram;
using Auth.Domain.Entities;
using Auth.Shared.Contracts;
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
    private readonly string _botToken;

    public TelegramBindModel(
        BindTelegramCommand bindTelegram,
        UserManager<UserEntity> userManager,
        IConfiguration cfg)
    {
        _bindTelegram = bindTelegram;
        _userManager = userManager;
        _botToken = cfg["Telegram:BotToken"] ?? "";
    }

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Error { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Message { get; set; }

    public void OnGet() { }

    [IgnoreAntiforgeryToken] // GET запрос от Telegram widget не имеет антифоргери токена
    public async Task<IActionResult> OnGetVerifyAsync(
        string id, string first_name, string last_name, string username,
        string photo_url, long auth_date, string hash, string? returnUrl)
    {
        // Получаем текущего пользователя через UserManager
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null)
        {
            // Если пользователь не авторизован, отправляем на логин
            return RedirectToPage("/Account/Login", new
            {
                returnUrl = Url.Page("/Account/Telegram/TelegramBind", new { returnUrl = returnUrl ?? "/" }),
                error = "not_authenticated"
            });
        }

        // Проверяем, активен ли пользователь
        if (!currentUser.IsActive)
        {
            return RedirectToPage("/Account/Telegram/TelegramBind", new
            {
                returnUrl = returnUrl,
                error = "Учетная запись неактивна"
            });
        }

        // Создаем DTO для привязки
        var dto = new TelegramRawData(
            Id: long.Parse(id),
            Username: username,
            FirstName: first_name,
            LastName: last_name,
            PhotoUrl: photo_url,
            AuthDate: auth_date,
            Hash: hash
        );

        // Выполняем привязку
        var result = await _bindTelegram.ExecuteAsync(
            currentUser.Id,
            dto,
            _botToken,
            CancellationToken.None);

        if (result.Success)
        {
            // Успешная привязка
            return RedirectToPage("/Account/Telegram/TelegramBind", new
            {
                returnUrl = returnUrl,
                message = "Telegram успешно привязан!"
            });
        }
        else
        {
            // Ошибка привязки
            return RedirectToPage("/Account/Telegram/TelegramBind", new
            {
                returnUrl = returnUrl,
                error = result.Error ?? "Не удалось привязать Telegram"
            });
        }
    }
}