using Auth.Application.Interfaces;
using Auth.Domain.Entities;
using Auth.Infrastructure.Data;
using Auth.Infrastructure.Services;
using Auth.Shared.Contracts;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Validation;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

public sealed class TelegramGrantValidator(
    AppDbContext db,
    UserManager<UserEntity> userManager,
    IConfiguration cfg,
    ITelegramAuthVerifier telegramAuthVerifier,
    ILogger<TelegramGrantValidator> log
) : IExtensionGrantValidator
{
    public string GrantType => GrantTypesConst.TelegramLogin;

    public async Task ValidateAsync(ExtensionGrantValidationContext ctx)
    {
        var idStr = ctx.Request.Raw.Get("id");
        var username = ctx.Request.Raw.Get("username");
        var firstName = ctx.Request.Raw.Get("first_name");
        var lastName = ctx.Request.Raw.Get("last_name");
        var photoUrl = ctx.Request.Raw.Get("photo_url");
        var authDate = ctx.Request.Raw.Get("auth_date");
        var hash = ctx.Request.Raw.Get("hash");

        log.LogInformation("TG grant raw: id={id} u={u} fn?={fn} ln?={ln} photo?={ph} t?={t} h?={h}",
            idStr, username, firstName is not null, lastName is not null, photoUrl is not null, authDate is not null, hash is not null);

        if (string.IsNullOrEmpty(idStr) || string.IsNullOrEmpty(authDate) || string.IsNullOrEmpty(hash))
        { ctx.Result = new(TokenRequestErrors.InvalidGrant, "missing fields"); log.LogWarning("TG grant fail: missing fields"); return; }

        if (!long.TryParse(idStr, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var id) ||
            !long.TryParse(authDate, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var authUnix))
        { ctx.Result = new(TokenRequestErrors.InvalidGrant, "bad numeric fields"); log.LogWarning("TG grant fail: bad numeric"); return; }

        var now = DateTimeOffset.UtcNow;
        var ts = DateTimeOffset.FromUnixTimeSeconds(authUnix);
        if (Math.Abs((now - ts).TotalSeconds) > 60)
        { ctx.Result = new(TokenRequestErrors.InvalidGrant, "stale request"); log.LogWarning("TG grant fail: stale request Δ={delta}s", (now - ts).TotalSeconds); return; }

        var botToken = cfg["Telegram:BotToken"];
        if (string.IsNullOrWhiteSpace(botToken))
        { ctx.Result = new(TokenRequestErrors.InvalidGrant, "server misconfigured: bot token"); log.LogError("TG grant fail: no bot token"); return; }

        var raw = new TelegramRawData(
            Id: id,
            Username: string.IsNullOrEmpty(username) ? null : username,
            FirstName: string.IsNullOrEmpty(firstName) ? null : firstName,
            LastName: string.IsNullOrEmpty(lastName) ? null : lastName,
            PhotoUrl: string.IsNullOrEmpty(photoUrl) ? null : photoUrl,
            AuthDate: authUnix,
            Hash: hash
        );

        if (!telegramAuthVerifier.Verify(raw, botToken))
        {
            // временно полезно увидеть DCS:
            var dcs = TelegramAuthVerifier.BuildDataCheckString(raw);
            log.LogWarning("TG grant fail: bad signature. DCS=`{dcs}`", dcs);
            ctx.Result = new(TokenRequestErrors.InvalidGrant, "bad signature");
            return;
        }

        var tg = await db.TelegramEntities.Include(t => t.User).FirstOrDefaultAsync(t => t.TelegramId == id);
        if (tg?.User is null || !tg.User.IsActive)
        { ctx.Result = new(TokenRequestErrors.InvalidGrant, "user not found"); log.LogWarning("TG grant fail: user not found or inactive for telegramId={id}", id); return; }

        ctx.Result = new GrantValidationResult(subject: tg.User.Id.ToString(), authenticationMethod: GrantType);
        tg.LastLoginDate = DateTime.UtcNow; await db.SaveChangesAsync();
        log.LogInformation("TG grant success: telegramId={id} userId={user}", id, tg.User.Id);
    }
}
