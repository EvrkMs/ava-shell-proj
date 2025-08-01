using System.Security.Cryptography;
using System.Text;
using TelegramService.DTOs;

namespace TelegramService.TelegramLinking;

public static class TelegramHashValidator
{
    public static bool Verify(TelegramLoginDto dto, string botToken)
    {
        var dict = new SortedDictionary<string, string>
        {
            ["auth_date"] = dto.auth_date,
            ["id"] = dto.id.ToString()
        };
        if (dto.username != null) dict["username"] = dto.username;
        if (dto.first_name != null) dict["first_name"] = dto.first_name;

        var dataCheckString = string.Join("\n", dict.Select(kv => $"{kv.Key}={kv.Value}"));

        // секрет = SHA256(botToken)
        var secret = SHA256.HashData(Encoding.UTF8.GetBytes(botToken));
        using var hmac = new HMACSHA256(secret);
        var calc = hmac.ComputeHash(Encoding.UTF8.GetBytes(dataCheckString));
        var hex = BitConverter.ToString(calc).Replace("-", "").ToLowerInvariant();

        return hex == dto.hash;
    }
}