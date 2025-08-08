using System.Text.Json.Serialization;

namespace Auth.Shared.Contracts;

public readonly record struct TelegramRawData(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("username")] string? Username,
    [property: JsonPropertyName("first_name")] string? FirstName,
    [property: JsonPropertyName("last_name")] string? LastName,
    [property: JsonPropertyName("photo_url")] string? PhotoUrl,
    [property: JsonPropertyName("auth_date")] long AuthDate,
    [property: JsonPropertyName("hash")] string Hash
);
