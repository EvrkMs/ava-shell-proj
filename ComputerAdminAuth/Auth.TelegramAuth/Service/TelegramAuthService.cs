// Namespace: Auth.TelegramAuth (библиотека)
using Auth.TelegramAuth.Interface;
using Auth.TelegramAuth.Options;
using Auth.TelegramAuth.Raw;
using System.Security.Cryptography;
using System.Text;

namespace Auth.TelegramAuth.Service;

public sealed class TelegramAuthService : ITelegramAuthService
{
    private readonly TelegramAuthOptions _opt;
    public TelegramAuthService(TelegramAuthOptions opt)
    {
        _opt = opt ?? throw new ArgumentNullException(nameof(opt));
        if (string.IsNullOrWhiteSpace(_opt.BotToken))
            throw new ArgumentException("BotToken is required", nameof(opt));
    }

    // ===== Login Widget =====
    public bool VerifyWidget(TelegramRawData dto, out string? error)
    {
        error = null;
        if (dto.Id <= 0 || dto.AuthDate <= 0 || string.IsNullOrWhiteSpace(dto.Hash))
        {
            error = "missing_fields"; return false;
        }

        if (!CheckTtl(dto.AuthDate, _opt.AllowedClockSkewSeconds))
        {
            error = "stale_request"; return false;
        }

        var dataCheck = BuildDataCheckString(new Dictionary<string, string?>
        {
            ["auth_date"] = dto.AuthDate.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["first_name"] = dto.FirstName,
            ["id"] = dto.Id.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["last_name"] = dto.LastName,
            ["photo_url"] = dto.PhotoUrl,
            ["username"] = dto.Username
        });

        // secret = SHA256(bot_token)
        var secret = SHA256.HashData(Encoding.UTF8.GetBytes(_opt.BotToken));
        var calc = HmacHex(secret, dataCheck);

        return ConstantTimeEquals(calc, dto.Hash);
    }

    // ===== WebApp initData =====

    public bool TryParseInitData(string initData, out Dictionary<string, string> data, out string hash, out string? error)
    {
        data = new(StringComparer.Ordinal);
        hash = string.Empty;
        error = null;

        if (string.IsNullOrWhiteSpace(initData)) { error = "initData_empty"; return false; }

        foreach (var p in initData.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var i = p.IndexOf('=');
            if (i <= 0) continue;
            var k = Uri.UnescapeDataString(p[..i]);
            var v = Uri.UnescapeDataString(p[(i + 1)..]);
            data[k] = v;
        }
        if (!data.TryGetValue("hash", out hash) || string.IsNullOrWhiteSpace(hash))
        {
            error = "hash_missing";
            return false;
        }
        return true;
    }

    public bool VerifyInitData(Dictionary<string, string> data, string hash, out string? error)
    {
        error = null;

        // TTL: auth_date обязателен
        if (!data.TryGetValue("auth_date", out var authDateStr) || !long.TryParse(authDateStr, out var auth))
        {
            error = "no_auth_date"; return false;
        }
        if (!CheckTtl(auth, _opt.AllowedClockSkewSeconds))
        {
            error = "stale_initData"; return false;
        }

        // secret = HMAC_SHA256("WebAppData", bot_token)
        using var hmac1 = new HMACSHA256(Encoding.UTF8.GetBytes("WebAppData"));
        var secret = hmac1.ComputeHash(Encoding.UTF8.GetBytes(_opt.BotToken));

        var dataCheck = BuildDataCheckString(
            data.Where(kv => !string.Equals(kv.Key, "hash", StringComparison.Ordinal))
                .ToDictionary(k => k.Key, v => v.Value, StringComparer.Ordinal)
        );

        var calc = HmacHex(secret, dataCheck);
        return ConstantTimeEquals(calc, hash);
    }

    // ===== Helpers =====
    private static bool CheckTtl(long authUnix, int skewSeconds)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        return Math.Abs(now - authUnix) <= Math.Max(30, skewSeconds);
    }

    private static string BuildDataCheckString(IDictionary<string, string?> dict) =>
        string.Join('\n',
            dict.Where(kv => !string.IsNullOrEmpty(kv.Value))
                .OrderBy(kv => kv.Key, StringComparer.Ordinal)
                .Select(kv => $"{kv.Key}={kv.Value}")
        );

    private static string HmacHex(byte[] key, string message)
    {
        using var h = new HMACSHA256(key);
        return Convert.ToHexString(h.ComputeHash(Encoding.UTF8.GetBytes(message))).ToLowerInvariant();
    }

    private static bool ConstantTimeEquals(string a, string b)
    {
        if (a.Length != b.Length) return false;
        var diff = 0;
        for (int i = 0; i < a.Length; i++) diff |= a[i] ^ b[i];
        return diff == 0;
    }
}
