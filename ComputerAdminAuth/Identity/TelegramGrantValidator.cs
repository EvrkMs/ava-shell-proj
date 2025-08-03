using System.Security.Cryptography;
using System.Text;
using ComputerAdminAuth.Data.Context;
using ComputerAdminAuth.Entities;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Validation;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ComputerAdminAuth.Identity;

public sealed class TelegramGrantValidator(
    AppDbContext db,
    UserManager<UserEntity> userManager,
    IConfiguration cfg) : IExtensionGrantValidator      // SignInManager удалён
{
    public string GrantType => "telegram_login";

    public async Task ValidateAsync(ExtensionGrantValidationContext ctx)
    {
        /* ---------- 0. Базовые поля ---------- */
        var idStr = ctx.Request.Raw.Get("id");
        var username = ctx.Request.Raw.Get("username");
        var firstName = ctx.Request.Raw.Get("first_name");
        var lastName = ctx.Request.Raw.Get("last_name");
        var photoUrl = ctx.Request.Raw.Get("photo_url");
        var authDate = ctx.Request.Raw.Get("auth_date");
        var hash = ctx.Request.Raw.Get("hash");

        if (string.IsNullOrEmpty(idStr) || string.IsNullOrEmpty(hash) ||
            !long.TryParse(idStr, out var id) ||
            !long.TryParse(authDate, out var authUnix))
        {
            ctx.Result = new(TokenRequestErrors.InvalidGrant, "missing fields");
            return;
        }

        /* ---------- 1. Тайм-аут ---------- */
        var authTime = DateTimeOffset.FromUnixTimeSeconds(authUnix);
        if ((DateTimeOffset.UtcNow - authTime).TotalSeconds > 60)
        {
            ctx.Result = new(TokenRequestErrors.InvalidGrant, "stale request");
            return;
        }

        /* ---------- 2. Проверка подписи ---------- */
        var raw = new TelegramRawData(
            Id: idStr,
            Username: username,
            FirstName: firstName ?? string.Empty,   // ← гарантируем not-null
            LastName: lastName,
            PhotoUrl: photoUrl,
            AuthDate: authDate!,
            Hash: hash);

        if (!VerifyHash(cfg["Telegram:BotToken"]!, raw))
        {
            ctx.Result = new(TokenRequestErrors.InvalidGrant, "bad signature");
            return;
        }

        /* ---------- 3. Поиск привязки ---------- */
        var tg = await db.TelegramEntities
                         .Include(t => t.User)
                         .SingleOrDefaultAsync(t => t.TelegramId == id);

        if (tg is null || tg.User is null)
        {
            ctx.Result = new(TokenRequestErrors.InvalidGrant,
                             "Пользователь с таким Telegram-ID не найден");
            return;
        }

        /* ---------- 4. Выдаём токен ---------- */
        ctx.Result = new GrantValidationResult(
            subject: tg.User.Id.ToString(), // Guid → string
            authenticationMethod: GrantType);

        // (Необязательно) обновляем дату последнего визита
        tg.LastLoginDate = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    /* ---------- Хэш-проверка без изменений ---------- */
    private static bool VerifyHash(string botToken, in TelegramRawData p)
    {
        var dataCheck = new[]
        {
            ("auth_date", p.AuthDate),
            ("first_name", p.FirstName),
            ("id",         p.Id),
            ("last_name",  p.LastName),
            ("photo_url",  p.PhotoUrl),
            ("username",   p.Username)
        }
        .Where(t => !string.IsNullOrEmpty(t.Item2))
        .OrderBy(t => t.Item1)
        .Select(t => $"{t.Item1}={t.Item2}");

        var data = string.Join('\n', dataCheck);
        var secretKey = SHA256.HashData(Encoding.UTF8.GetBytes(botToken));

        using var hmac = new HMACSHA256(secretKey);
        var calc = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(data)))
                         .ToLowerInvariant();

        return calc == p.Hash;
    }
}

public readonly record struct TelegramRawData(
    string Id,
    string? Username,
    string FirstName,
    string? LastName,
    string? PhotoUrl,
    string AuthDate,
    string Hash);
