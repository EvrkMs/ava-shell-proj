using Auth.Application.Interfaces;
using Auth.Domain.Entities;
using Auth.TelegramAuth.Interface;
using Auth.TelegramAuth.Raw;

namespace Auth.Application.UseCases.Telegram;

public class BindTelegramCommand
{
    private readonly ITelegramRepository _telegramRepo;
    private readonly IUserRepository _userRepo;
    private readonly ITelegramAuthService _tg;
    private readonly IUnitOfWork _unitOfWork;

    public BindTelegramCommand(
        IUnitOfWork unitOfWork,
        ITelegramRepository telegramRepo,
        IUserRepository userRepo,
        ITelegramAuthService tg)
    {
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _telegramRepo = telegramRepo ?? throw new ArgumentNullException(nameof(telegramRepo));
        _userRepo = userRepo ?? throw new ArgumentNullException(nameof(userRepo));
        _tg = tg ?? throw new ArgumentNullException(nameof(tg));
    }

    public async Task<Result> ExecuteAsync(
        Guid currentUserId,
        TelegramRawData dto,
        CancellationToken ct = default)
    {
        // 1) Валидация
        if (!_tg.VerifyWidget(dto, out var err))
            return Result.Fail(err ?? "bad signature");

        // 2) Проверка существующей привязки
        var existingByTg = await _telegramRepo.GetByTelegramIdAsync(dto.Id, ct);
        if (existingByTg is not null && existingByTg.UserId != currentUserId)
            return Result.Fail("Этот Telegram уже привязан к другому аккаунту");

        // 3) Проверка пользователя
        var user = await _userRepo.GetByIdAsync(currentUserId, ct);
        if (user is null || !user.IsActive)
            return Result.Fail("Пользователь не найден или не активен");

        // 4) Нормализация username
        var username = (dto.Username ?? string.Empty).Trim();
        if (username.StartsWith("@")) username = username[1..];
        username = username.ToLowerInvariant();

        // 5) Создаём/обновляем привязку
        var myTg = await _telegramRepo.GetByUserIdAsync(currentUserId, ct);

        try
        {
            if (myTg is null)
            {
                // СОЗДАНИЕ - простая операция, транзакция НЕ нужна
                var entity = new TelegramEntity
                {
                    Id = Guid.NewGuid(), // Если нужен
                    TelegramId = dto.Id,
                    FirstName = dto.FirstName ?? string.Empty,
                    LastName = dto.LastName ?? string.Empty,
                    Username = username,
                    PhotoUrl = dto.PhotoUrl ?? string.Empty,
                    UserId = currentUserId,
                    BoundAt = DateTime.UtcNow,
                    LastLoginDate = DateTime.UtcNow
                };

                await _telegramRepo.AddAsync(entity);  // ✅ Синхронный метод
                await _unitOfWork.SaveChangesAsync(ct);  // ✅ Сохраняем через UnitOfWork
            }
            else
            {
                // ОБНОВЛЕНИЕ - тоже простая операция
                myTg.FirstName = dto.FirstName ?? myTg.FirstName;
                myTg.LastName = dto.LastName ?? myTg.LastName;
                myTg.Username = string.IsNullOrEmpty(username) ? myTg.Username : username;
                myTg.PhotoUrl = dto.PhotoUrl ?? myTg.PhotoUrl;
                myTg.LastLoginDate = DateTime.UtcNow;

                await _telegramRepo.UpdateAsync(myTg);  // ✅ Синхронный метод
                await _unitOfWork.SaveChangesAsync(ct);  // ✅ Сохраняем через UnitOfWork
            }

            return Result.Ok();
        }
        catch (Exception ex)
        {
            // Логирование было бы полезно
            return Result.Fail($"Не удалось сохранить привязку: {ex.Message}");
        }
    }
}