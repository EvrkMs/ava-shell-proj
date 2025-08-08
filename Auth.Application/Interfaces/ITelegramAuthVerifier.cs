namespace Auth.Application.Interfaces;

public interface ITelegramAuthVerifier
{
    bool Verify(Shared.Contracts.TelegramRawData dto, string botToken);
}