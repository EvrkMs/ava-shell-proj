using System.Security.Cryptography;
using System.Text;
using ComputerAdminAuth.Controller;

namespace ComputerAdminAuth.Helpers;

// Helpers/TelegramVerifier.cs
public static class TelegramVerifier
{
    public static bool Verify(TelegramDto dto, string botToken)
    {
        var pairs = new[]
        {
        ("auth_date",  dto.auth_date.ToString()),
        ("first_name", dto.first_name),
        ("id",         dto.id.ToString()),
        ("last_name",  dto.last_name ?? ""),
        ("photo_url",  dto.photo_url ?? ""),
        ("username",   dto.username  ?? "")
    }
        .Where(p => !string.IsNullOrEmpty(p.Item2))
        .OrderBy(p => p.Item1)
        .Select(p => $"{p.Item1}={p.Item2}");

        var data = string.Join('\n', pairs);
        var secretKey = SHA256.HashData(Encoding.UTF8.GetBytes(botToken));

        using var hmac = new HMACSHA256(secretKey);
        var calc = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(data)))
                         .ToLowerInvariant();

        return calc == dto.hash;
    }
}
