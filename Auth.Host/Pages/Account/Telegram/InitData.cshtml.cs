using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Linq;
using Auth.Application.Interfaces;   // ITelegramRepository
using Auth.Domain.Entities;        // UserEntity
using Auth.Infrastructure;         // CustomSignInManager
using Duende.IdentityServer.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Auth.Host.Pages.Account.Telegram
{
    [AllowAnonymous]
    [IgnoreAntiforgeryToken] // POST идёт из WebView без антифорджери
    public class InitDataModel : PageModel
    {
        private readonly ILogger<InitDataModel> _log;
        private readonly IIdentityServerInteractionService _interaction;
        private readonly IConfiguration _cfg;
        private readonly CustomSignInManager _signIn;       // SignInManager<UserEntity>
        private readonly ITelegramRepository _telegramRepo; // ваш репозиторий Telegram

        public InitDataModel(
            ILogger<InitDataModel> log,
            IIdentityServerInteractionService interaction,
            IConfiguration cfg,
            CustomSignInManager signIn,
            ITelegramRepository telegramRepo)
        {
            _log = log;
            _interaction = interaction;
            _cfg = cfg;
            _signIn = signIn;
            _telegramRepo = telegramRepo;
        }

        public sealed class InitDataRequest
        {
            [Required] public string initData { get; set; } = string.Empty;
            public string? returnUrl { get; set; }
        }

        [HttpPost]
        public async Task<IActionResult> OnPostAsync([FromBody] InitDataRequest req)
        {
            if (!ModelState.IsValid)
                return BadRequest(new { error = "invalid request" });

            var botToken = _cfg["Telegram:BotToken"];
            if (string.IsNullOrWhiteSpace(botToken))
                return StatusCode(500, new { error = "BotToken not configured" });

            // 1) Разбор и проверка подписи initData по правилам WebApp (Mini App)
            if (!TryParseInitData(req.initData, out var dict, out var hash))
                return StatusCode(401, new { error = "malformed initData" });

            if (!VerifyInitData(dict, hash, botToken))
                return StatusCode(401, new { error = "bad signature" });

            // 2) TTL (auth_date обязателен)
            if (!dict.TryGetValue("auth_date", out var authDateStr) || !long.TryParse(authDateStr, out var authDate))
                return StatusCode(401, new { error = "no auth_date" });

            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var maxSkew = Math.Max(30, _cfg.GetValue<int?>("Telegram:WebAppSkewSeconds") ?? 300); // ≤5 минут
            if (Math.Abs(now - authDate) > maxSkew)
                return StatusCode(401, new { error = "stale initData" });

            // 3) Достаём user из initData
            if (!dict.TryGetValue("user", out var userJson) || string.IsNullOrWhiteSpace(userJson))
                return StatusCode(401, new { error = "no user payload" });

            var tgUser = JsonSerializer.Deserialize<TgUser>(userJson);
            if (tgUser is null || tgUser.id <= 0)
                return StatusCode(401, new { error = "bad user payload" });

            // 4) Находим связку Telegram -> User (репозиторий должен подгрузить User через Include)
            var tg = await _telegramRepo.GetByTelegramIdAsync(tgUser.id);
            var appUser = tg?.User;
            if (appUser is null || !appUser.IsActive)
                return StatusCode(401, new { error = "user not found" });

            // 5) Требование смены пароля — уводим на анонимную страницу смены
            if (appUser.MustChangePassword)
            {
                var safeReturn = SafeReturn(req.returnUrl);
                var url = Url.Page("/Account/ChangePassword", new
                {
                    userName = appUser.UserName,
                    returnUrl = safeReturn,
                    requireChange = true
                });
                return new JsonResult(new { redirect = url });
            }

            // 6) Успешный вход
            await _signIn.SignInAsync(appUser, isPersistent: true);

            // 7) Безопасный редирект назад
            var redirect = SafeReturn(req.returnUrl) ?? "/";
            return new JsonResult(new { redirect });
        }

        private string? SafeReturn(string? returnUrl)
            => !string.IsNullOrWhiteSpace(returnUrl) && _interaction.IsValidReturnUrl(returnUrl)
                ? returnUrl : "/";

        // === Модель user из initData ===
        private sealed class TgUser
        {
            public long id { get; set; }
            public string? username { get; set; }
            public string? first_name { get; set; }
            public string? last_name { get; set; }
            public string? language_code { get; set; }
            public bool? is_premium { get; set; }
        }

        // === Валидация initData для WebApp (Mini App) ===
        // Алгоритм (важно не перепутать с Login Widget):
        //  1) secret = HMAC_SHA256(key="WebAppData", message=bot_token)
        //  2) data_check_string = join(<key>=<value> для всех ключей КРОМЕ 'hash',
        //                              отсортированных по ключу), разделитель '\n'
        //  3) calc_hash = HMAC_SHA256(key=secret, message=data_check_string) -> hex lower
        //  4) сравнить с hash из initData в константное время
        private static bool VerifyInitData(Dictionary<string, string> data, string hash, string botToken)
        {
            using var hmac1 = new HMACSHA256(Encoding.UTF8.GetBytes("WebAppData"));
            var secret = hmac1.ComputeHash(Encoding.UTF8.GetBytes(botToken));

            var lines = data
                .Where(kv => !string.Equals(kv.Key, "hash", StringComparison.Ordinal))
                .OrderBy(kv => kv.Key, StringComparer.Ordinal)
                .Select(kv => $"{kv.Key}={kv.Value}");
            var dataCheckString = string.Join("\n", lines);

            using var hmac2 = new HMACSHA256(secret);
            var calc = hmac2.ComputeHash(Encoding.UTF8.GetBytes(dataCheckString));
            var calcHex = Convert.ToHexString(calc).ToLowerInvariant();

            return ConstantTimeEquals(calcHex, hash);
        }

        private static bool TryParseInitData(string initData, out Dictionary<string, string> dict, out string hash)
        {
            dict = new(StringComparer.Ordinal);
            hash = string.Empty;

            // initData — querystring-подобная строка "key=value&key2=value2..."
            var parts = initData.Split('&', StringSplitOptions.RemoveEmptyEntries);
            foreach (var p in parts)
            {
                var i = p.IndexOf('=');
                if (i <= 0) continue;
                var k = Uri.UnescapeDataString(p[..i]);
                var v = Uri.UnescapeDataString(p[(i + 1)..]);
                dict[k] = v;
            }
            dict.TryGetValue("hash", out hash);
            return dict.Count > 0 && !string.IsNullOrWhiteSpace(hash);
        }

        private static bool ConstantTimeEquals(string a, string b)
        {
            if (a.Length != b.Length) return false;
            var diff = 0;
            for (int i = 0; i < a.Length; i++)
                diff |= a[i] ^ b[i];
            return diff == 0;
        }
    }
}
