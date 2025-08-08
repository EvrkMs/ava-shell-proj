namespace Auth.Application.Interfaces;

public interface ITelegramAuthVerifier
{
    bool Verify(Shared.Contracts.TelegramDto dto, string botToken);
}