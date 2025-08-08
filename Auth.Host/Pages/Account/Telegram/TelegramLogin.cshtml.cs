using Auth.Application.Interfaces; // ITelegramAuthVerifier
using Auth.Infrastructure; // CustomSignInManager
using Auth.Infrastructure.Data; // AppDbContext
using Auth.Infrastructure.Telegram; // TelegramAuthOptions
using Auth.Shared.Contracts;
using Duende.IdentityServer.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Auth.Host.Pages.Account.Telegram;

[AllowAnonymous]
[IgnoreAntiforgeryToken]               // виджет шлёт GET без антифорджери
[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
public class TelegramLoginModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly CustomSignInManager _signInManager;
    private readonly IIdentityServerInteractionService _interaction;
    private readonly ITelegramAuthVerifier _verifier;
    private readonly TelegramAuthOptions _opts;
    private readonly ILogger<TelegramLoginModel> _log;

    public TelegramLoginModel(
        AppDbContext db,
        CustomSignInManager signInManager,
        IIdentityServerInteractionService interaction,
        ITelegramAuthVerifier verifier,
        TelegramAuthOptions opts,
        ILogger<TelegramLoginModel> log)
    {
        _db = db;
        _signInManager = signInManager;
        _interaction = interaction;
        _verifier = verifier;
        _opts = opts;
        _log = log;
    }

    // Прямой биндинг query от виджета + наш returnUrl
    public class TelegramQuery
    {
        [FromQuery(Name = "id")] public long Id { get; set; }
        [FromQuery(Name = "username")] public string? Username { get; set; }
        [FromQuery(Name = "first_name")] public string? FirstName { get; set; }
        [FromQuery(Name = "last_name")] public string? LastName { get; set; }
        [FromQuery(Name = "photo_url")] public string? PhotoUrl { get; set; }
        [FromQuery(Name = "auth_date")] public long AuthDate { get; set; }
        [FromQuery(Name = "hash")] public string Hash { get; set; } = string.Empty;

        // возвращаемся обратно в OIDC authorize (или домашку), если было
        [FromQuery(Name = "returnUrl")] public string? ReturnUrl { get; set; }
    }

    public async Task<IActionResult> OnGetAsync([FromQuery] TelegramQuery q)
    {
        // 1) sanity-check
        if (q.Id <= 0 || q.AuthDate <= 0 || string.IsNullOrWhiteSpace(q.Hash))
        {
            _log.LogWarning("TelegramLogin: missing fields (id={id}, auth_date={auth}, hash?={hash})",
                q.Id, q.AuthDate, !string.IsNullOrEmpty(q.Hash));
            return RedirectToPage("Login", new { returnUrl = q.ReturnUrl, error = "missing fields" });
        }

        // 2) TTL с допуском
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var skew = Math.Max(5, _opts.AllowedClockSkewSeconds);
        if (Math.Abs(now - q.AuthDate) > skew)
        {
            _log.LogWarning("TelegramLogin: stale request Δ={delta}s > {skew}s", Math.Abs(now - q.AuthDate), skew);
            return RedirectToPage("Login", new { returnUrl = q.ReturnUrl, error = "stale request" });
        }

        // 3) Подпись
        if (string.IsNullOrWhiteSpace(_opts.BotToken))
        {
            _log.LogError("TelegramLogin: BotToken is not configured");
            return RedirectToPage("Login", new { returnUrl = q.ReturnUrl, error = "server misconfigured" });
        }

        var raw = new TelegramRawData(
            Id: q.Id,
            Username: string.IsNullOrWhiteSpace(q.Username) ? null : q.Username,
            FirstName: string.IsNullOrWhiteSpace(q.FirstName) ? null : q.FirstName,
            LastName: string.IsNullOrWhiteSpace(q.LastName) ? null : q.LastName,
            PhotoUrl: string.IsNullOrWhiteSpace(q.PhotoUrl) ? null : q.PhotoUrl,
            AuthDate: q.AuthDate,
            Hash: q.Hash
        );

        if (!_verifier.Verify(raw, _opts.BotToken))
        {
            _log.LogWarning("TelegramLogin: bad signature for id={id}", q.Id);
            return RedirectToPage("Login", new { returnUrl = q.ReturnUrl, error = "bad signature" });
        }

        var tg = await _db.TelegramEntities
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.TelegramId == q.Id);

        if (tg?.User is null || !tg.User.IsActive)
        {
            return RedirectToPage("Login", new { returnUrl = q.ReturnUrl, error = "user not found" });
        }

        // >>> ДО SignInAsync – вставь проверку <<<
        if (tg.User is { } u && u.MustChangePassword) // имя свойства подставь своё
        {
            var safeReturn = (!string.IsNullOrWhiteSpace(q.ReturnUrl) && _interaction.IsValidReturnUrl(q.ReturnUrl))
                ? q.ReturnUrl
                : "/";

            // уводим на анонимную ChangePassword (как делали из Login)
            return RedirectToPage("/Account/ChangePassword", new
            {
                userName = u.UserName,
                returnUrl = safeReturn,
                requireChange = true
            });
        }

        // 5) Вход
        await _signInManager.SignInAsync(tg.User, isPersistent: true);

        // 6) Возврат в authorize, если там начиналось
        if (!string.IsNullOrWhiteSpace(q.ReturnUrl) && _interaction.IsValidReturnUrl(q.ReturnUrl))
        {
            _log.LogInformation("TelegramLogin: success (userId={userId}) -> returnUrl", tg.User.Id);
            return LocalRedirect(q.ReturnUrl!);
        }

        _log.LogInformation("TelegramLogin: success (userId={userId}) -> /", tg.User.Id);
        return LocalRedirect("/");
    }
}
