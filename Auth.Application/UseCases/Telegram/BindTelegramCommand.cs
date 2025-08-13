using Auth.Application;
using Auth.Application.Interfaces;
using Auth.Domain.Entities;
using Auth.TelegramAuth.Interface;
using Auth.TelegramAuth.Raw;
// Result — как у тебя в Application (Auth.Application\UseCases\Telegram\Result.cs)

namespace Auth.Application.UseCases.Telegram;

public class BindTelegramCommand
{
    private readonly ITelegramRepository _telegramRepo;
    private readonly IUserRepository _userRepo;
    private readonly ITelegramAuthService _tg; // новый сервис валидации

    public BindTelegramCommand(
        ITelegramRepository telegramRepo,
        IUserRepository userRepo,
        ITelegramAuthService tg)
    {
        _telegramRepo = telegramRepo ?? throw new ArgumentNullException(nameof(telegramRepo));
        _userRepo = userRepo ?? throw new ArgumentNullException(nameof(userRepo));
        _tg = tg ?? throw new ArgumentNullException(nameof(tg));
    }

    /// <summary>
    /// Привязка Telegram-аккаунта к текущему пользователю по данным Login Widget.
    /// dto — это параметры с колбэка виджета (id, username, first_name, last_name, photo_url, auth_date, hash).
    /// </summary>
    public async Task<Result> ExecuteAsync(
        Guid currentUserId,
        TelegramRawData dto,
        CancellationToken ct = default)
    {
        // 1) Криптографическая валидация Login Widget (TTL, подпись, формат).
        if (!_tg.VerifyWidget(dto, out var err))
            return Result.Fail(err ?? "bad signature");

        // 2) TG-ID уже у другого пользователя?
        //    ITelegramRepository — твой текущий контракт.  ✔
        var existingByTg = await _telegramRepo.GetByTelegramIdAsync(dto.Id, ct); // :contentReference[oaicite:0]{index=0}
        if (existingByTg is not null && existingByTg.UserId != currentUserId)
            return Result.Fail("Этот Telegram уже привязан к другому аккаунту");

        // 3) Проверим пользователя
        var user = await _userRepo.GetByIdAsync(currentUserId, ct); // :contentReference[oaicite:1]{index=1}
        if (user is null || !user.IsActive) // IsActive — у тебя есть в сущности.  ✔
            return Result.Fail("Пользователь не найден или не активен"); // :contentReference[oaicite:2]{index=2}

        // 4) Нормализуем username
        var username = (dto.Username ?? string.Empty).Trim();
        if (username.StartsWith("@")) username = username[1..];
        username = username.ToLowerInvariant();

        // 5) Создаём/обновляем привязку
        var myTg = await _telegramRepo.GetByUserIdAsync(currentUserId, ct); // :contentReference[oaicite:3]{index=3}
        if (myTg is null)
        {
            var entity = new TelegramEntity
            {
                TelegramId = dto.Id,
                FirstName = dto.FirstName ?? string.Empty,
                LastName = dto.LastName ?? string.Empty,
                Username = username,
                PhotoUrl = dto.PhotoUrl ?? string.Empty,
                UserId = currentUserId,
                BoundAt = DateTime.UtcNow,
                LastLoginDate = DateTime.UtcNow
            };

            try
            {
                await _telegramRepo.AddAsync(entity, ct); // :contentReference[oaicite:4]{index=4}
            }
            catch
            {
                return Result.Fail("Не удалось сохранить привязку (конфликт). Повторите попытку.");
            }
        }
        else
        {
            myTg.FirstName = dto.FirstName ?? myTg.FirstName;
            myTg.LastName = dto.LastName ?? myTg.LastName;
            myTg.Username = string.IsNullOrEmpty(username) ? myTg.Username : username;
            myTg.PhotoUrl = dto.PhotoUrl ?? myTg.PhotoUrl;
            myTg.LastLoginDate = DateTime.UtcNow;

            try
            {
                await _telegramRepo.UpdateAsync(myTg, ct); // :contentReference[oaicite:5]{index=5}
            }
            catch
            {
                return Result.Fail("Не удалось обновить привязку.");
            }
        }

        return Result.Ok();
    }
}
