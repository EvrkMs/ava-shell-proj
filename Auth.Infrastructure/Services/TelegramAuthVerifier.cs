using System.Security.Cryptography;
using System.Text;
using Auth.Application.Interfaces;
using Auth.Shared.Contracts;

namespace Auth.Infrastructure.Services;

public class TelegramAuthVerifier : ITelegramAuthVerifier
{
    public bool Verify(TelegramDto telegramDto, string botToken)
    {
        if (string.IsNullOrEmpty(botToken))
            return false;

        // Сбор полей кроме hash
        var data = new List<string>();

        if (telegramDto.auth_date != 0)
            data.Add($"auth_date={telegramDto.auth_date}");
        if (!string.IsNullOrEmpty(telegramDto.first_name))
            data.Add($"first_name={telegramDto.first_name}");
        if (telegramDto.id != 0)
            data.Add($"id={telegramDto.id}");
        if (!string.IsNullOrEmpty(telegramDto.photo_url))
            data.Add($"photo_url={telegramDto.photo_url}");
        if (!string.IsNullOrEmpty(telegramDto.username))
            data.Add($"username={telegramDto.username}");

        // Сортировка по ключу
        data.Sort(StringComparer.Ordinal);

        var dataCheckString = string.Join("\n", data);

        // SHA256(botToken)
        using var sha256 = SHA256.Create();
        var secretKey = sha256.ComputeHash(Encoding.UTF8.GetBytes(botToken));

        // HMAC-SHA256(dataCheckString, secretKey)
        using var hmac = new HMACSHA256(secretKey);
        var hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(dataCheckString));
        var hashHex = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();

        return hashHex == telegramDto.hash.ToLowerInvariant();
    }
}
