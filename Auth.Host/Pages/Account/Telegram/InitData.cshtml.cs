using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Linq; // не забудь
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
    [IgnoreAntiforgeryToken]
    public class InitDataModel : PageModel
    {
        private readonly ILogger<InitDataModel> _log;
        private readonly IIdentityServerInteractionService _interaction;
        private readonly IConfiguration _cfg;
        private readonly CustomSignInManager _signIn;
        private readonly ITelegramRepository _telegramRepo;

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
            if (!ModelState.IsValid) return Fail("invalid request", req.returnUrl, 400);

            var botToken = _cfg["Telegram:BotToken"];
            if (string.IsNullOrWhiteSpace(botToken)) return Fail("server misconfigured", req.returnUrl, 500);

            if (!TryParseInitData(req.initData, out var dict, out var hash))
                return Fail("malformed initData", req.returnUrl, 401);

            if (!VerifyInitData(dict, hash, botToken))
                return Fail("bad signature", req.returnUrl, 401);

            if (!dict.TryGetValue("auth_date", out var authDateStr) || !long.TryParse(authDateStr, out var authDate))
                return Fail("no auth_date", req.returnUrl, 401);

            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var maxSkew = Math.Max(30, _cfg.GetValue<int?>("Telegram:WebAppSkewSeconds") ?? 300);
            if (Math.Abs(now - authDate) > maxSkew)
                return Fail("stale initData", req.returnUrl, 401);

            if (!dict.TryGetValue("user", out var userJson) || string.IsNullOrWhiteSpace(userJson))
                return Fail("no user payload", req.returnUrl, 401);

            var tgUser = JsonSerializer.Deserialize<TgUser>(userJson);
            if (tgUser is null || tgUser.id <= 0)
                return Fail("bad user payload", req.returnUrl, 401);

            var tg = await _telegramRepo.GetByTelegramIdAsync(tgUser.id);
            var appUser = tg?.User;
            if (appUser is null || !appUser.IsActive)
                return Fail("user not found", req.returnUrl, 401);

            if (appUser.MustChangePassword)
            {
                var safeReturn = SafeReturn(req.returnUrl);
                var url = Url.Page("/Account/ChangePassword", new
                {
                    userName = appUser.UserName,
                    returnUrl = safeReturn,
                    requireChange = true
                });
                return OkRedirect(url); // 200 с redirect
            }

            await _signIn.SignInAsync(appUser, isPersistent: true);
            return OkRedirect(SafeReturn(req.returnUrl) ?? "/");
        }

        // ХЕЛПЕРЫ

        private JsonResult OkRedirect(string url) =>
            new JsonResult(new { redirect = url }) { StatusCode = 200 };

        private JsonResult Fail(string code, string? returnUrl, int status) =>
            new JsonResult(new
            {
                error = code,
                // ВСЕГДА сохраняем исходный returnUrl при возврате на Login!
                redirect = Url.Page("/Account/Login", pageHandler: null,
                                    values: new { returnUrl = returnUrl ?? "/" },
                                    protocol: Request.Scheme)
            })
            { StatusCode = status };

        private string? SafeReturn(string? returnUrl) =>
            !string.IsNullOrWhiteSpace(returnUrl) && _interaction.IsValidReturnUrl(returnUrl) ? returnUrl : "/";

        private sealed class TgUser
        {
            public long id { get; set; }
            public string? username { get; set; }
            public string? first_name { get; set; }
            public string? last_name { get; set; }
            public string? language_code { get; set; }
            public bool? is_premium { get; set; }
        }

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
            var calcHex = Convert.ToHexString(hmac2.ComputeHash(Encoding.UTF8.GetBytes(dataCheckString))).ToLowerInvariant();
            return ConstantTimeEquals(calcHex, hash);
        }

        private static bool TryParseInitData(string initData, out Dictionary<string, string> dict, out string hash)
        {
            dict = new(StringComparer.Ordinal);
            hash = string.Empty;

            foreach (var p in initData.Split('&', StringSplitOptions.RemoveEmptyEntries))
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
            for (int i = 0; i < a.Length; i++) diff |= a[i] ^ b[i];
            return diff == 0;
        }
    }
}
