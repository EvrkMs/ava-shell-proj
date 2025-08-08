namespace Auth.Shared.Contracts;

public record TelegramDto(
    long id,
    string username,
    string first_name,
    string? photo_url,
    long auth_date,
    string hash);
