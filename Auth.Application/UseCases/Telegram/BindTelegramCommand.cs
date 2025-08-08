using Auth.Application.Interfaces;
using Auth.Application.UseCases.Telegram.Utils;
using Auth.Domain.Entities;
using Auth.Shared.Contracts;

namespace Auth.Application.UseCases.Telegram;

public class BindTelegramCommand
{
    private readonly ITelegramRepository _telegramRepo;
    private readonly IUserRepository _userRepo;
    private readonly ITelegramAuthVerifier _verifier;
    private readonly ITelegramPayloadValidator _payloadValidator;

    public BindTelegramCommand(
        ITelegramRepository telegramRepo,
        IUserRepository userRepo,
        ITelegramAuthVerifier verifier,
        ITelegramPayloadValidator payloadValidator)
    {
        _telegramRepo = telegramRepo;
        _userRepo = userRepo;
        _verifier = verifier;
        _payloadValidator = payloadValidator;
    }

    /// <summary>
    /// botToken передаём из контроллера (Infrastructure) — Application не знает про секреты/опции.
    /// </summary>
    public async Task<Result> ExecuteAsync(
        Guid currentUserId,
        TelegramRawData dto,
        string botToken,
        CancellationToken ct = default)
    {
        // 0) TTL / базовая валидация (общая логика, без знания конфигов)
        var ttlCheck = _payloadValidator.ValidateBasics(dto.Id, dto.AuthDate, skewSeconds: 60);
        if (ttlCheck != TelegramPayloadCheck.Ok)
        {
            var reason = ttlCheck switch
            {
                TelegramPayloadCheck.Stale => "stale auth_date",
                TelegramPayloadCheck.BadNumeric => "bad numeric fields",
                TelegramPayloadCheck.MissingFields => "missing fields",
                _ => "invalid"
            };
            return Result.Fail(reason);
        }

        // 1) Подпись (секрет отдаёт контроллер)
        if (!_verifier.Verify(dto, botToken))
            return Result.Fail("bad signature");

        // 2) TG-ID уже у другого пользователя?
        var existingByTg = await _telegramRepo.GetByTelegramIdAsync(dto.Id, ct);
        if (existingByTg is not null && existingByTg.UserId != currentUserId)
            return Result.Fail("Этот Telegram уже привязан к другому аккаунту");

        // 3) Пользователь валиден/активен?
        var user = await _userRepo.GetByIdAsync(currentUserId, ct);
        if (user is null || !user.IsActive)
            return Result.Fail("Пользователь не найден или не активен");

        // 4) Нормализуем username
        var username = (dto.Username ?? string.Empty).Trim();
        if (username.StartsWith("@")) username = username[1..];
        username = username.ToLowerInvariant();

        // 5) Создаём/обновляем привязку
        var myTg = await _telegramRepo.GetByUserIdAsync(currentUserId, ct);
        if (myTg is null)
        {
            var entity = new TelegramEntity
            {
                TelegramId = dto.Id,
                FirstName = dto.FirstName,
                Username = username,
                PhotoUrl = dto.PhotoUrl ?? "",
                UserId = currentUserId,
                BoundAt = DateTime.UtcNow,
                LastLoginDate = DateTime.UtcNow
            };

            try
            {
                await _telegramRepo.AddAsync(entity, ct);
            }
            catch
            {
                return Result.Fail("Не удалось сохранить привязку (конфликт). Повторите попытку.");
            }
        }
        else
        {
            myTg.FirstName = dto.FirstName ?? myTg.FirstName;
            myTg.Username = string.IsNullOrEmpty(username) ? myTg.Username : username;
            myTg.PhotoUrl = dto.PhotoUrl ?? myTg.PhotoUrl;
            myTg.LastLoginDate = DateTime.UtcNow;

            try
            {
                await _telegramRepo.UpdateAsync(myTg, ct);
            }
            catch
            {
                return Result.Fail("Не удалось обновить привязку.");
            }
        }

        return Result.Ok();
    }
}
