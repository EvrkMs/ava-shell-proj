
// --- using-и ---
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TelegramService.Data;
using TelegramService.DTOs;
using TelegramService.TelegramLinking;
using IdentityModel.AspNetCore.OAuth2Introspection;   // события интроспекции
using Microsoft.AspNetCore.HttpLogging;               // HTTP-logging

var builder = WebApplication.CreateBuilder(args);
var cfg = builder.Configuration;
var svcs = builder.Services;

// ---------- 1. ЛОГИРОВАНИЕ ----------
builder.Logging.ClearProviders();
builder.Logging.AddConsole();         // stdout
builder.Logging.AddDebug();           // VS Output
builder.Logging
       .AddFilter("Microsoft", LogLevel.Warning)   // шум ниже
       .AddFilter("System", LogLevel.Warning)
       .AddFilter("Duende", LogLevel.Debug)     // токены / интроспекция
       .AddFilter("IdentityModel", LogLevel.Debug)
       .AddFilter("TelegramService", LogLevel.Debug);    // наш namespace

// (опционально) HTTP-logging middleware
svcs.AddHttpLogging(o =>
{
    o.LoggingFields = HttpLoggingFields.RequestMethod
                   | HttpLoggingFields.RequestPath
                   | HttpLoggingFields.ResponseStatusCode;
    o.RequestHeaders.Add("Authorization");             // ⚠️ убирать в продакшене
});

// ---------- 2. ХРАНИЛИЩЕ ----------
svcs.AddDbContext<AppDbContext>(o =>
    o.UseNpgsql(cfg.GetConnectionString("DefaultConnection"))
     .LogTo(Console.WriteLine, LogLevel.Information)   // EF Core SQL
     .EnableSensitiveDataLogging());                   // ⚠️ dev-режим

// ---------- 3. АУТЕНТИФИКАЦИЯ (интроспекция) ----------
svcs.AddAuthentication("Bearer")
    .AddOAuth2Introspection("Bearer", o =>
    {
        o.Authority = "https://dns.ava-kk.com/auth";
        o.ClientId = "telegram-introspection";
        o.ClientSecret = "super-secret";

        // ----- события -----
        o.Events = new OAuth2IntrospectionEvents
        {
            OnSendingRequest = ctx =>
            {
                Console.WriteLine(ctx.HttpContext.Request.ToString());
            },
            OnTokenValidated = ctx =>
            {
                var log = ctx.HttpContext.RequestServices
                             .GetRequiredService<ILoggerFactory>()
                             .CreateLogger("Introspection");
                Console.WriteLine(ctx.HttpContext.Request.ToString());

                var sub = ctx.Principal?.FindFirst("sub")?.Value;
                log.LogDebug("✅ Token OK for sub {Sub}", sub);

                return Task.CompletedTask;
            },

            OnAuthenticationFailed = ctx =>
            {
                var log = ctx.HttpContext.RequestServices
                             .GetRequiredService<ILoggerFactory>()
                             .CreateLogger("Introspection");
                Console.WriteLine(ctx.HttpContext.Request.ToString());
                // токен берём из заголовка, укорачиваем для лога
                var raw = ctx.HttpContext.Request.Headers.Authorization.ToString();
                var token = raw.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                            ? raw[7..] : raw;

                log.LogWarning(ctx.Error,
                    "❌ Introspection failed. Token: {Tok}. Error: {Err}",
                    Short(token),
                    ctx.Error);

                return Task.CompletedTask;
            }
        };
    });
svcs.AddAuthorization();

// ---------- 4. НАСТРОЙКИ БОТА ----------
svcs.Configure<TelegramBotOptions>(cfg.GetSection("Telegram"));     // BotToken

// ---------- 5. HTTP PIPELINE ----------
builder.WebHost.UseKestrel(opt => opt.ListenAnyIP(5002, k => k.UseHttps()));

var app = builder.Build();
app.UsePathBase("/telega");
app.UseHttpLogging();                         //  ← после PathBase
app.UseAuthentication();
app.UseAuthorization();

// ---------- 6. ENDPOINT /bind ----------
app.MapPost("/bind", async (
        TelegramLoginDto dto,
        AppDbContext db,
        IOptions<TelegramBotOptions> bot,
        ClaimsPrincipal user,
        ILogger<Program> log) =>   // <-- логгер внедрён
{
    if (!user.Identity?.IsAuthenticated ?? true)
        return Results.Unauthorized();

    if (!TelegramHashValidator.Verify(dto, bot.Value.BotToken))
    {
        log.LogWarning("hash_invalid for tgId {TgId}", dto.id);
        return Results.BadRequest("hash_invalid");
    }

    var userId = Guid.Parse(user.FindFirstValue("sub")!);
    log.LogInformation("Bind attempt: appUser {UserId} -> tg {TgId}",
                       userId, dto.id);

    var link = await db.TelegramLinks
                       .FirstOrDefaultAsync(x => x.UserId == userId);

    if (link is null)
        db.TelegramLinks.Add(link = new TelegramLink { UserId = userId });

    link.TelegramId = dto.id;
    link.TelegramName = dto.username ?? dto.first_name ?? string.Empty;
    link.BoundAt = DateTime.UtcNow;

    await db.SaveChangesAsync();

    log.LogInformation("Bind success for {UserId}", userId);
    return Results.Ok();
})
.WithName("BindTelegram")
.RequireAuthorization();

app.MapGet("/", () => "Telega Service OK");
app.Run();

// ---------- 7. ВСПОМОГАТЕЛЬНОЕ ----------
static string Short(string t) => string.IsNullOrEmpty(t) || t.Length <= 10
                                ? t
                                : $"{t[..5]}…{t[^5..]}";

/* --- options --- */
public record TelegramBotOptions { public string BotToken { get; init; } = ""; }
