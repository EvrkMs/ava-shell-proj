using System.Globalization;
using Auth.Application.Interfaces;
using Auth.Application.UseCases.Telegram.Utils;
using Auth.Domain.Entities;
using Auth.Infrastructure.Data;
using Auth.Infrastructure.Telegram;
using Auth.Shared.Contracts;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Validation;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Auth.Infrastructure;

public sealed class TelegramGrantValidator(
    AppDbContext db,
    UserManager<UserEntity> userManager,            // можно удалить, если точно не нужен
    TelegramAuthOptions opts,                       // опции/секреты — это инфраструктура
    ITelegramAuthVerifier telegramAuthVerifier,
    ITelegramPayloadValidator payloadValidator,     // абстракция из Application.Interfaces
    ILogger<TelegramGrantValidator> log
) : IExtensionGrantValidator
{
    public string GrantType => GrantTypesConst.TelegramLogin;

    public async Task ValidateAsync(ExtensionGrantValidationContext ctx)
    {
        // 0) raw
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
        {
            ctx.Result = new(TokenRequestErrors.InvalidGrant, "missing fields");
            log.LogWarning("TG grant fail: missing fields");
            return;
        }

        if (!long.TryParse(idStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) ||
            !long.TryParse(authDate, NumberStyles.Integer, CultureInfo.InvariantCulture, out var authUnix))
        {
            ctx.Result = new(TokenRequestErrors.InvalidGrant, "bad numeric fields");
            log.LogWarning("TG grant fail: bad numeric fields");
            return;
        }

        // 1) TTL через общий валидатор, но окно — из инфраструктурных опций
        var ttlCheck = payloadValidator.ValidateBasics(id, authUnix, opts.AllowedClockSkewSeconds);
        if (ttlCheck != TelegramPayloadCheck.Ok)
        {
            var reason = ttlCheck switch
            {
                TelegramPayloadCheck.Stale => "stale request",
                TelegramPayloadCheck.BadNumeric => "bad numeric fields",
                TelegramPayloadCheck.MissingFields => "missing fields",
                _ => "invalid"
            };
            ctx.Result = new(TokenRequestErrors.InvalidGrant, reason);
            log.LogWarning("TG grant fail: {reason} (Δ={delta}s)",
                reason, Math.Abs(DateTimeOffset.UtcNow.ToUnixTimeSeconds() - authUnix));
            return;
        }

        if (string.IsNullOrWhiteSpace(opts.BotToken))
        {
            ctx.Result = new(TokenRequestErrors.InvalidGrant, "server misconfigured: bot token");
            log.LogError("TG grant fail: no bot token in options");
            return;
        }

        var raw = new TelegramRawData(
            Id: id,
            Username: string.IsNullOrWhiteSpace(username) ? null : username,
            FirstName: string.IsNullOrWhiteSpace(firstName) ? null : firstName,
            LastName: string.IsNullOrWhiteSpace(lastName) ? null : lastName,
            PhotoUrl: string.IsNullOrWhiteSpace(photoUrl) ? null : photoUrl,
            AuthDate: authUnix,
            Hash: hash
        );

        if (!telegramAuthVerifier.Verify(raw, opts.BotToken))
        {
            // На проде лучше не логировать DCS.
            // var dcs = TelegramAuthVerifier.BuildDataCheckString(raw);
            // log.LogWarning("TG grant fail: bad signature. DCS=`{dcs}`", dcs);

            ctx.Result = new(TokenRequestErrors.InvalidGrant, "bad signature");
            log.LogWarning("TG grant fail: bad signature for telegramId={id}", id);
            return;
        }

        var tg = await db.TelegramEntities
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.TelegramId == id);

        if (tg?.User is null || !tg.User.IsActive)
        {
            ctx.Result = new(TokenRequestErrors.InvalidGrant, "user not found");
            log.LogWarning("TG grant fail: user not found or inactive for telegramId={id}", id);
            return;
        }

        // 2) Ограничим скоупы разрешёнными в опциях (мягко)
        var requestedScopes = (ctx.Request.Raw.Get("scope") ?? string.Empty)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var finalScopes = requestedScopes
            .Intersect(opts.AllowedScopes, StringComparer.Ordinal)
            .ToList();
        ctx.Request.RequestedScopes = finalScopes;

        // 3) успех
        ctx.Result = new GrantValidationResult(
            subject: tg.User.Id.ToString(),
            authenticationMethod: GrantType);

        tg.LastLoginDate = DateTime.UtcNow;
        await db.SaveChangesAsync();

        log.LogInformation("TG grant success: telegramId={id} userId={user}", id, tg.User.Id);
    }
}
