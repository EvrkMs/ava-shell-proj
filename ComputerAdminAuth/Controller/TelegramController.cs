namespace ComputerAdminAuth.Controller;

using System.Security.Claims;

// Controllers/TelegramController.cs
using ComputerAdminAuth.Data.Context;
using ComputerAdminAuth.Entities;
using ComputerAdminAuth.Helpers;
using ComputerAdminAuth.Services;
using Duende.IdentityModel;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[ApiController, Route("api/telegram")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]// ⬅️ Bearer-токен обязателен
[IgnoreAntiforgeryToken]
public class TelegramController(
        AppDbContext db,
        IConfiguration cfg,
        ILogger<TelegramController> log) : ControllerBase
{
    private readonly string _botToken = cfg["Telegram:BotToken"]!;

    /* ---------- POST /api/telegram/bind ---------- */
    [HttpPost("bind")]
    public async Task<IActionResult> Bind([FromBody] TelegramDto dto)
    {
        /* 1. Подпись + ttl */
        if (!TelegramVerifier.Verify(dto, _botToken))
            return BadRequest("bad signature");

        if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() - dto.auth_date > 60)
            return BadRequest("stale auth_date");

        /* 2. Проверяем, не занято ли уже */
        var exists = await db.TelegramEntities
                             .AsNoTracking()
                             .AnyAsync(t => t.TelegramId == dto.id);
        if (exists)
            return Conflict("Этот Telegram уже привязан к другому аккаунту");

        /* 3. Берём current user */
        var userId = Guid.Parse(User.FindFirstValue(JwtClaimTypes.Subject)!);

        /* 4. Сохраняем */
        var entity = new TelegramEntity
        {
            TelegramId = dto.id,
            FirstName = dto.first_name,
            LastName = dto.last_name ?? "",
            Username = dto.username ?? "",
            PhotoUrl = dto.photo_url ?? "",
            UserId = userId,
            BoundAt = DateTime.UtcNow,
            LastLoginDate = DateTime.UtcNow
        };
        db.TelegramEntities.Add(entity);
        await db.SaveChangesAsync();

        log.LogInformation("User {UserId} bound Telegram {Tg}", userId, dto.id);
        return Ok(new { message = "bound" });
    }
    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        var userId = Guid.Parse(User.FindFirstValue(JwtClaimTypes.Subject)!);
        var tg = await db.TelegramEntities
                         .AsNoTracking()
                         .FirstOrDefaultAsync(t => t.UserId == userId);

        return tg is null ? NotFound() : Ok(tg);
    }
    /* ---------- POST /api/telegram/unbind ---------- */
    [HttpPost("unbind")]
    public async Task<IActionResult> Unbind()
    {
        var userId = Guid.Parse(User.FindFirstValue(JwtClaimTypes.Subject)!);

        var row = await db.TelegramEntities
                          .FirstOrDefaultAsync(t => t.UserId == userId);
        if (row is null) return NotFound("not bound");

        db.TelegramEntities.Remove(row);
        await db.SaveChangesAsync();

        return Ok(new { message = "unbound" });
    }
}

// Contracts/TelegramDto.cs
public record TelegramDto(
    long id,
    string username,
    string first_name,
    string? last_name,
    string? photo_url,
    long auth_date,
    string hash);
