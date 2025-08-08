using System.Text.Encodings.Web;
using Auth.Application.UseCases.Telegram;
using Auth.Shared.Contracts;
using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Auth.Host.Pages.Account.Telegram;

[Authorize]
[ValidateAntiForgeryToken]
public class TelegramBindModel : PageModel
{
    private readonly BindTelegramCommand _bindTelegram;
    private readonly IIdentityServerInteractionService _interaction;
    private readonly string _botToken;

    public TelegramBindModel(
        BindTelegramCommand bindTelegram,
        IIdentityServerInteractionService interaction,
        IConfiguration cfg)
    {
        _bindTelegram = bindTelegram;
        _interaction = interaction;
        _botToken = cfg["Telegram:BotToken"] ?? "";
    }

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Error { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Message { get; set; }

    public void OnGet() { }

    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> OnGetVerifyAsync(
    string id, string first_name, string last_name, string username,
    string photo_url, long auth_date, string hash, string? returnUrl)
    {
        // Здесь выполняем команду бинда
        var dto = new TelegramRawData(
            Id: long.Parse(id),
            Username: username,
            FirstName: first_name,
            LastName: last_name,
            PhotoUrl: photo_url,
            AuthDate: auth_date,
            Hash: hash
        );

        var userId = User.GetSubjectId();
        if (string.IsNullOrEmpty(userId))
        {
            return Redirect($"/Account/Login?returnUrl={UrlEncoder.Default.Encode(returnUrl ?? "/")}");
        }

        var result = await _bindTelegram.ExecuteAsync(Guid.Parse(userId!), dto, _botToken, CancellationToken.None);

        if (result.Success)
        {
            return Redirect(returnUrl ?? "/");
        }
        else
        {
            return Redirect($"{returnUrl ?? "/"}?error={Uri.EscapeDataString(result.Error ?? "Bind failed")}");
        }
    }
}
