using System.Runtime.CompilerServices;

namespace Auth.Host.Configuration;

internal static class DotEnv
{
    [ModuleInitializer]
    public static void Init()
    {
        try
        {
            var candidates = new[]
            {
                Path.Combine(Directory.GetCurrentDirectory(), ".env"),
                Path.Combine(AppContext.BaseDirectory, ".env")
            };
            foreach (var path in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (!File.Exists(path)) continue;
                foreach (var rawLine in File.ReadAllLines(path))
                {
                    var line = rawLine.Trim();
                    if (line.Length == 0 || line.StartsWith('#')) continue;
                    if (line.StartsWith("export ", StringComparison.Ordinal))
                        line = line[7..].TrimStart();
                    var idx = line.IndexOf('=');
                    if (idx <= 0) continue;
                    var key = line[..idx].Trim();
                    var val = line[(idx + 1)..].Trim();
                    if ((val.StartsWith('"') && val.EndsWith('"')) || (val.StartsWith('\'') && val.EndsWith('\'')))
                        val = val[1..^1];
                    if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(key)))
                        Environment.SetEnvironmentVariable(key, val);
                }
            }
        }
        catch
        {
            // ignore .env loading errors
        }
    }
}

