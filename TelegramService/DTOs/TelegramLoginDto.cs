namespace TelegramService.DTOs;

public record TelegramLoginDto(
    long id,
    string hash,
    string? username,
    string? first_name,
    string auth_date);

