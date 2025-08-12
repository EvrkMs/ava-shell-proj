using Auth.Application.Interfaces;
using Auth.Domain.Entities;
using Auth.Infrastructure;
using Auth.Infrastructure.Telegram;
using Auth.Shared.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Auth.Host.Pages.Account.Telegram
{
    [AllowAnonymous]
    [IgnoreAntiforgeryToken]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public class TelegramLoginModel : PageModel
    {
        private readonly ITelegramRepository _telegramRepo;
        private readonly UserManager<UserEntity> _userManager;
        private readonly CustomSignInManager _signInManager;
        private readonly ITelegramAuthVerifier _verifier;
        private readonly TelegramAuthOptions _opts;
        private readonly ILogger<TelegramLoginModel> _log;

        public TelegramLoginModel(
            ITelegramRepository telegramRepo,
            UserManager<UserEntity> userManager,
            CustomSignInManager signInManager,
            ITelegramAuthVerifier verifier,
            TelegramAuthOptions opts,
            ILogger<TelegramLoginModel> log)
        {
            _telegramRepo = telegramRepo;
            _userManager = userManager;
            _signInManager = signInManager;
            _verifier = verifier;
            _opts = opts;
            _log = log;
        }

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
            // 1) Валидация полей
            if (q.Id <= 0 || q.AuthDate <= 0 || string.IsNullOrWhiteSpace(q.Hash))
            {
                _log.LogWarning("TelegramLogin: missing fields (id={id}, auth_date={auth}, hash?={hash})",
                    q.Id, q.AuthDate, !string.IsNullOrEmpty(q.Hash));
                return RedirectToPage("/Account/Login", new { returnUrl = q.ReturnUrl, error = "missing_fields" });
            }

            // 2) Проверка TTL
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var skew = Math.Max(60, _opts.AllowedClockSkewSeconds);
            if (Math.Abs(now - q.AuthDate) > skew)
            {
                _log.LogWarning("TelegramLogin: stale request Δ={delta}s > {skew}s",
                    Math.Abs(now - q.AuthDate), skew);
                return RedirectToPage("/Account/Login", new { returnUrl = q.ReturnUrl, error = "stale_request" });
            }

            // 3) Проверка конфигурации
            if (string.IsNullOrWhiteSpace(_opts.BotToken))
            {
                _log.LogError("TelegramLogin: BotToken is not configured");
                return RedirectToPage("/Account/Login", new { returnUrl = q.ReturnUrl, error = "server_error" });
            }

            // 4) Проверка подписи
            var raw = new TelegramRawData(
                Id: q.Id,
                Username: q.Username,
                FirstName: q.FirstName,
                LastName: q.LastName,
                PhotoUrl: q.PhotoUrl,
                AuthDate: q.AuthDate,
                Hash: q.Hash
            );

            if (!_verifier.Verify(raw, _opts.BotToken))
            {
                _log.LogWarning("TelegramLogin: bad signature for id={id}", q.Id);
                return RedirectToPage("/Account/Login", new { returnUrl = q.ReturnUrl, error = "invalid_signature" });
            }

            // 5) Ищем привязку через репозиторий
            var telegram = await _telegramRepo.GetByTelegramIdAsync(q.Id);
            if (telegram == null)
            {
                _log.LogWarning("TelegramLogin: no binding for telegram_id={id}", q.Id);
                return RedirectToPage("/Account/Login", new { returnUrl = q.ReturnUrl, error = "no_binding" });
            }

            // 6) Получаем пользователя через UserManager
            var user = await _userManager.FindByIdAsync(telegram.UserId.ToString());
            if (user == null)
            {
                _log.LogWarning("TelegramLogin: user not found for id={userId}", telegram.UserId);
                return RedirectToPage("/Account/Login", new { returnUrl = q.ReturnUrl, error = "user_not_found" });
            }

            // 7) Проверяем статус пользователя
            if (!user.IsActive)
            {
                _log.LogWarning("TelegramLogin: user {userId} is not active", user.Id);
                return RedirectToPage("/Account/Login", new { returnUrl = q.ReturnUrl, error = "user_inactive" });
            }

            // 8) Проверяем необходимость смены пароля
            if (user.MustChangePassword)
            {
                _log.LogInformation("TelegramLogin: user {userId} must change password", user.Id);
                return RedirectToPage("/Account/ChangePassword", new
                {
                    userName = user.UserName,
                    returnUrl = SafeReturn(q.ReturnUrl),
                    requireChange = true
                });
            }

            // 9) Обновляем дату последнего входа
            telegram.LastLoginDate = DateTime.UtcNow;
            await _telegramRepo.UpdateAsync(telegram);

            // 10) Выполняем вход
            await _signInManager.SignInAsync(user, isPersistent: true);

            // 11) Редирект
            var targetUrl = SafeReturn(q.ReturnUrl);
            _log.LogInformation("TelegramLogin: success for user {userId}, redirecting to {url}",
                user.Id, targetUrl);

            return LocalRedirect(targetUrl);
        }

        private string SafeReturn(string? url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return "/";

            if (!Url.IsLocalUrl(url))
                return "/";

            return url;
        }
    }
}