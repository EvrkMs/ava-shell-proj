namespace Auth.Application.UseCases.Telegram;

using Auth.Application;
using Auth.Application.Interfaces;

public class UnbindTelegramCommand
{
    private readonly ITelegramRepository _telegramRepo;

    public UnbindTelegramCommand(ITelegramRepository telegramRepo)
    {
        _telegramRepo = telegramRepo;
    }

    public async Task<Result> ExecuteAsync(Guid currentUserId, CancellationToken ct = default)
    {
        var tg = await _telegramRepo.GetByUserIdAsync(currentUserId, ct);
        if (tg == null)
            return Result.Fail("not bound");

        await _telegramRepo.RemoveAsync(tg, ct);
        return Result.Ok();
    }
}
