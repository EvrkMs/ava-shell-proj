// Namespace: Auth.TelegramAuth (библиотека)
using Auth.TelegramAuth.Raw;

namespace Auth.TelegramAuth.Interface;

public interface ITelegramAuthService
{
    bool VerifyWidget(TelegramRawData dto, out string? error);
    bool TryParseInitData(string initData, out Dictionary<string, string> data, out string hash, out string? error);
    bool VerifyInitData(Dictionary<string, string> data, string hash, out string? error);
}
