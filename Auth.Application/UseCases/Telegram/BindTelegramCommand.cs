namespace Auth.Application.UseCases.Telegram;

using Auth.Application;
using Auth.Application.Interfaces;
using Auth.Domain.Entities;
using Auth.Shared.Contracts;

public class BindTelegramCommand
{
    private readonly ITelegramRepository _telegramRepo;
    private readonly IUserRepository _userRepo;
    private readonly ITelegramAuthVerifier _verifier;

    public BindTelegramCommand(
        ITelegramRepository telegramRepo,
        IUserRepository userRepo,
        ITelegramAuthVerifier verifier)
    {
        _telegramRepo = telegramRepo;
        _userRepo = userRepo;
        _verifier = verifier;
    }

    public async Task<Result> ExecuteAsync(Guid currentUserId, TelegramRawData dto, string botToken, CancellationToken ct = default)
    {
        // 1. Подпись
        if (!_verifier.Verify(dto, botToken))
            return Result.Fail("bad signature");

        // 2. TTL
        if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() - dto.AuthDate > 60)
            return Result.Fail("stale auth_date");

        // 3. Проверка уникальности TelegramId
        if (await _telegramRepo.ExistsByTelegramIdAsync(dto.Id, ct))
            return Result.Fail("Этот Telegram уже привязан к другому аккаунту");

        // 4. Проверка пользователя
        var user = await _userRepo.GetByIdAsync(currentUserId, ct);
        if (user == null || !user.IsActive)
            return Result.Fail("Пользователь не найден или не активен");

        // 5. Сохранение
        var entity = new TelegramEntity
        {
            TelegramId = dto.Id,
            FirstName = dto.FirstName,
            Username = dto.Username ?? "",
            PhotoUrl = dto.PhotoUrl ?? "",
            UserId = currentUserId,
            BoundAt = DateTime.UtcNow,
            LastLoginDate = DateTime.UtcNow
        };

        await _telegramRepo.AddAsync(entity, ct);
        return Result.Ok();
    }
}
