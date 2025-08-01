using System.Security.Cryptography;
using System.Text;

namespace TelegramService.Service;
public static class TelegramLoginValidator
{
    public static bool CheckHash(IReadOnlyDictionary<string, string> data, string botToken)
    {
        var hash = data["hash"];
        var sortedData = data
            .Where(kv => kv.Key != "hash")
            .OrderBy(kv => kv.Key)
            .Select(kv => $"{kv.Key}={kv.Value}");
        var dataCheckString = string.Join("\n", sortedData);

        using var sha256 = new HMACSHA256(Encoding.UTF8.GetBytes(botToken));
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(dataCheckString));
        var hexHash = Convert.ToHexStringLower(hashBytes);

        return hash == hexHash;
    }
}

