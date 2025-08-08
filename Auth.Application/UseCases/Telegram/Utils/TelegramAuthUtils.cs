using System.Runtime.CompilerServices;
using Auth.Shared.Contracts;

namespace Auth.Application.UseCases.Telegram.Utils;

public static class TelegramAuthUtils
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string? NullIfEmpty(string? s) =>
        string.IsNullOrWhiteSpace(s) ? null : s;

    public static TelegramRawData Normalize(long id, long authDate, string hash,
        string? username, string? firstName, string? lastName, string? photoUrl) =>
        new(
            Id: id,
            Username: NullIfEmpty(username),
            FirstName: NullIfEmpty(firstName),
            LastName: NullIfEmpty(lastName),
            PhotoUrl: NullIfEmpty(photoUrl),
            AuthDate: authDate,
            Hash: hash
        );
}
public enum TelegramPayloadCheck
{
    Ok,
    MissingFields,
    BadNumeric,
    Stale
}

public interface ITelegramPayloadValidator
{
    TelegramPayloadCheck ValidateBasics(long id, long authUnix, int skewSeconds);
}

public sealed class TelegramPayloadValidator : ITelegramPayloadValidator
{
    public TelegramPayloadCheck ValidateBasics(long id, long authUnix, int skewSeconds)
    {
        if (id <= 0 || authUnix <= 0) return TelegramPayloadCheck.BadNumeric;
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (Math.Abs(now - authUnix) > skewSeconds) return TelegramPayloadCheck.Stale;
        return TelegramPayloadCheck.Ok;
    }
}
