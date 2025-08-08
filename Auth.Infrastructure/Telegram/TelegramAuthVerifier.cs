using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Auth.Application.Interfaces;
using Auth.Shared.Contracts;

namespace Auth.Infrastructure.Telegram;

public class TelegramAuthVerifier : ITelegramAuthVerifier
{
    public bool Verify(TelegramRawData p, string botToken)
    {
        if (string.IsNullOrWhiteSpace(botToken))
            return false;

        // Собираем пары (только непустые), сортируем по ключу (Ordinal), склеиваем через '\n'
        // Важно: id/auth_date -> строка через InvariantCulture
        var pairs = new (string k, string? v)[]
        {
            ("auth_date", p.AuthDate == 0 ? null : p.AuthDate.ToString(CultureInfo.InvariantCulture)),
            ("first_name", string.IsNullOrEmpty(p.FirstName) ? null : p.FirstName),
            ("id",        p.Id == 0 ? null : p.Id.ToString(CultureInfo.InvariantCulture)),
            ("last_name", string.IsNullOrEmpty(p.LastName) ? null : p.LastName),
            ("photo_url", string.IsNullOrEmpty(p.PhotoUrl) ? null : p.PhotoUrl),
            ("username",  string.IsNullOrEmpty(p.Username) ? null : p.Username),
        };

        var sb = new StringBuilder(capacity: 128);
        foreach (var s in pairs
            .Where(x => !string.IsNullOrEmpty(x.v))
            .OrderBy(x => x.k, StringComparer.Ordinal)
            .Select(x => $"{x.k}={x.v}"))
        {
            if (sb.Length > 0) sb.Append('\n');
            sb.Append(s);
        }

        var dataBytes = Encoding.UTF8.GetBytes(sb.ToString());

        // Ключ = SHA256(botToken)
        var key = SHA256.HashData(Encoding.UTF8.GetBytes(botToken));

        // HMAC-SHA256(data_check_string, key)
        using var hmac = new HMACSHA256(key);
        var calc = hmac.ComputeHash(dataBytes);

        // Сравниваем в константном времени
        byte[]? given;
        try
        {
            given = Convert.FromHexString(p.Hash);
        }
        catch
        {
            return false; // невалидный hex
        }

        return given.Length == calc.Length &&
               CryptographicOperations.FixedTimeEquals(calc, given);
    }
    public static string BuildDataCheckString(TelegramRawData p)
    {
        var pairs = new (string k, string? v)[]
        {
        ("auth_date", p.AuthDate.ToString(CultureInfo.InvariantCulture)),
        ("first_name", p.FirstName),
        ("id", p.Id.ToString(CultureInfo.InvariantCulture)),
        ("last_name", p.LastName),
        ("photo_url", p.PhotoUrl),
        ("username", p.Username)
        };
        return string.Join('\n', pairs.Where(x => !string.IsNullOrEmpty(x.v))
            .OrderBy(x => x.k, StringComparer.Ordinal)
            .Select(x => $"{x.k}={x.v}"));
    }

}
