using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Auth.Application.Interfaces;     // ITelegramRepository
using Auth.Infrastructure;             // CustomSignInManager
using Auth.TelegramAuth.Interface;     // ITelegramAuthService
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
        private readonly CustomSignInManager _signIn;
        private readonly ITelegramRepository _telegramRepo;
        private readonly ITelegramAuthService _tg;

        public InitDataModel(
            ILogger<InitDataModel> log,
            CustomSignInManager signIn,
            ITelegramRepository telegramRepo,
            ITelegramAuthService tg)
        {
            _log = log;
            _signIn = signIn;
            _telegramRepo = telegramRepo;
            _tg = tg;
        }

        public sealed class InitDataRequest
        {
            [Required] public string initData { get; set; } = string.Empty;
            public string? returnUrl { get; set; }
        }

        public async Task<IActionResult> OnPostAsync([FromBody] InitDataRequest req, CancellationToken ct)
        {
            if (!ModelState.IsValid)
                return Fail("invalid_request", req.returnUrl, 400);

            // 1) Разберём initData
            if (!_tg.TryParseInitData(req.initData, out var data, out var hash, out var parseErr))
                return Fail(parseErr ?? "malformed_initData", req.returnUrl, 401);

            // 2) Проверим подпись + TTL (внутри сервис сам знает пороги)
            if (!_tg.VerifyInitData(data, hash, out var verifyErr))
                return Fail(verifyErr ?? "bad_signature", req.returnUrl, 401);

            // 3) user payload
            if (!data.TryGetValue("user", out var userJson) || string.IsNullOrWhiteSpace(userJson))
                return Fail("no_user_payload", req.returnUrl, 401);

            var tgUser = JsonSerializer.Deserialize<TgUser>(userJson);
            if (tgUser is null || tgUser.id <= 0)
                return Fail("bad_user_payload", req.returnUrl, 401);

            // 4) Привязка к пользователю
            var tg = await _telegramRepo.GetByTelegramIdAsync(tgUser.id, ct);
            var appUser = tg?.User;
            if (appUser is null || !appUser.IsActive)
                return Fail("user_not_found", req.returnUrl, 401);

            if (appUser.MustChangePassword)
            {
                var safeReturn = SafeReturn(req.returnUrl);
                var url = Url.Page("/Account/ChangePassword", new
                {
                    userName = appUser.UserName,
                    returnUrl = safeReturn,
                    requireChange = true
                });
                return OkRedirect(url);
            }

            await _signIn.SignInAsync(appUser, isPersistent: true);
            return OkRedirect(SafeReturn(req.returnUrl) ?? "/");
        }

        // === Helpers ===
        private JsonResult OkRedirect(string url) =>
            new JsonResult(new { redirect = url }) { StatusCode = 200 };

        private JsonResult Fail(string code, string? returnUrl, int status) =>
            new JsonResult(new
            {
                error = code,
                redirect = Url.Page("/Account/Login", pageHandler: null,
                                    values: new { returnUrl = returnUrl ?? "/", error = code },
                                    protocol: Request.Scheme)
            })
            { StatusCode = status };

        private string SafeReturn(string? url)
            => (!string.IsNullOrWhiteSpace(url) && Url.IsLocalUrl(url)) ? url! : "/";

        private sealed class TgUser
        {
            public long id { get; set; }
            public string? username { get; set; }
            public string? first_name { get; set; }
            public string? last_name { get; set; }
            public string? language_code { get; set; }
            public bool? is_premium { get; set; }
        }
    }
}
