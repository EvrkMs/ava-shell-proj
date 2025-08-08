using System.Globalization;
using Auth.Domain.Entities;
using Auth.Infrastructure.Telegram; // TelegramAuthOptions
using Auth.Shared.Contracts;
using Duende.IdentityServer.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Auth.Infrastructure.Data; // ваш DbContext
using Auth.Application.Interfaces; // ITelegramAuthVerifier
using Microsoft.AspNetCore.Authorization;

namespace Auth.Host.Pages.Account;

[AllowAnonymous]
[IgnoreAntiforgeryToken] // виджет идёт GET без антифорджери
public class TelegramLoginModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly SignInManager<UserEntity> _signInManager;
    private readonly IIdentityServerInteractionService _interaction;
    private readonly ITelegramAuthVerifier _verifier;
    private readonly TelegramAuthOptions _opts;

    public TelegramLoginModel(
        AppDbContext db,
        SignInManager<UserEntity> signInManager,
        IIdentityServerInteractionService interaction,
        ITelegramAuthVerifier verifier,
        TelegramAuthOptions opts)
    {
        _db = db;
        _signInManager = signInManager;
        _interaction = interaction;
        _verifier = verifier;
        _opts = opts;
    }

    // Модель для биндинга QueryString от виджета
    public class TelegramQuery
    {
        [FromQuery(Name = "id")] public long Id { get; set; }
        [FromQuery(Name = "username")] public string? Username { get; set; }
        [FromQuery(Name = "first_name")] public string? FirstName { get; set; }
        [FromQuery(Name = "last_name")] public string? LastName { get; set; }
        [FromQuery(Name = "photo_url")] public string? PhotoUrl { get; set; }
        [FromQuery(Name = "auth_date")] public long AuthDate { get; set; }
        [FromQuery(Name = "hash")] public string Hash { get; set; } = string.Empty;

        [FromQuery(Name = "returnUrl")] public string? ReturnUrl { get; set; }
    }

    public async Task<IActionResult> OnGetAsync([FromQuery] TelegramQuery q)
    {
        // 1) Мини-валидация
        if (q.Id <= 0 || q.AuthDate <= 0 || string.IsNullOrWhiteSpace(q.Hash))
        {
            return RedirectToPage("Login", new { returnUrl = q.ReturnUrl, error = "missing fields" });
        }

        // 2) TTL
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var delta = Math.Abs(now - q.AuthDate);
        var skew = Math.Max(5, _opts.AllowedClockSkewSeconds); // защита от нулей
        if (delta > skew)
        {
            return RedirectToPage("Login", new { returnUrl = q.ReturnUrl, error = "stale request" });
        }

        // 3) Проверка подписи
        var raw = new TelegramRawData(
            Id: q.Id,
            Username: string.IsNullOrWhiteSpace(q.Username) ? null : q.Username,
            FirstName: string.IsNullOrWhiteSpace(q.FirstName) ? null : q.FirstName,
            LastName: string.IsNullOrWhiteSpace(q.LastName) ? null : q.LastName,
            PhotoUrl: string.IsNullOrWhiteSpace(q.PhotoUrl) ? null : q.PhotoUrl,
            AuthDate: q.AuthDate,
            Hash: q.Hash
        );

        if (string.IsNullOrWhiteSpace(_opts.BotToken) || !_verifier.Verify(raw, _opts.BotToken))
        {
            return RedirectToPage("Login", new { returnUrl = q.ReturnUrl, error = "bad signature" });
        }

        // 4) Ищем привязку TG→User
        var tg = await _db.TelegramEntities
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.TelegramId == q.Id);

        if (tg?.User is null || !tg.User.IsActive)
        {
            // Пользователь не найден — можно редиректить на bind-поток или показать ошибку
            return RedirectToPage("Login", new { returnUrl = q.ReturnUrl, error = "user not found" });
        }

        // 5) Логиним: создаём auth cookie
        await _signInManager.SignInAsync(tg.User, isPersistent: true);

        // 6) Валидируем и возвращаемся в authorize-поток
        if (!string.IsNullOrWhiteSpace(q.ReturnUrl) && _interaction.IsValidReturnUrl(q.ReturnUrl))
        {
            return LocalRedirect(q.ReturnUrl!);
        }

        return LocalRedirect("/");
    }
}
