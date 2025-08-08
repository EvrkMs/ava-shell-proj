using Auth.Application.UseCases.Telegram;
using Duende.IdentityServer.Extensions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Auth.Host.Controllers;

[ApiController]
[Route("api/telegram")]
[IgnoreAntiforgeryToken]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = "ApiRead")]
public class TelegramController : ControllerBase
{
    private readonly BindTelegramCommand _bindTelegram;
    private readonly UnbindTelegramCommand _unbindTelegram;
    private readonly GetMyTelegramQuery _getMyTelegram;
    private readonly string _botToken;
    private readonly ILogger<TelegramController> _logger;

    public TelegramController(
        BindTelegramCommand bindTelegram,
        UnbindTelegramCommand unbindTelegram,
        GetMyTelegramQuery getMyTelegram,
        IConfiguration cfg,
        ILogger<TelegramController> logger)
    {
        _bindTelegram = bindTelegram;
        _unbindTelegram = unbindTelegram;
        _getMyTelegram = getMyTelegram;
        _botToken = cfg["Telegram:BotToken"]!;
        _logger = logger;
    }

    [HttpGet("me")]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        var sub = User.GetSubjectId();

        if (string.IsNullOrEmpty(sub))
            return Unauthorized("no subject in token");

        var userId = Guid.Parse(sub!);
        var tg = await _getMyTelegram.ExecuteAsync(userId, ct);
        return tg is null ? NotFound() : Ok(tg);
    }

    [HttpPost("unbind")]
    public async Task<IActionResult> Unbind(CancellationToken ct)
    {
        var sub = User.GetSubjectId();
        if (string.IsNullOrEmpty(sub))
            return Unauthorized("no subject in token");

        var userId = Guid.Parse(sub!);
        var result = await _unbindTelegram.ExecuteAsync(userId, ct);
        return result.Success
            ? Ok(new { message = "unbound" })
            : NotFound(result.Error);
    }
}
