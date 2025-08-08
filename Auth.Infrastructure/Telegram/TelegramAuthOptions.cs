namespace Auth.Infrastructure.Telegram;

public sealed class TelegramAuthOptions
{
    public string BotToken { get; init; } = "";
    public int AllowedClockSkewSeconds { get; init; } = 60;
    public string[] AllowedScopes { get; init; } = ["openid", "profile", "api", "offline_access"];
}

