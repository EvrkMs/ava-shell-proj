using System.ComponentModel.DataAnnotations;

namespace Auth.Domain.Entities;

public class UserSession
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid UserId { get; set; }

    // OIDC client id that initiated the session (optional)
    public string? ClientId { get; set; }

    // Metadata for UX/admin
    public string? Device { get; set; }
    public string? UserAgent { get; set; }
    public string? IpAddress { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastSeenAt { get; set; }
    public DateTime? ExpiresAt { get; set; }

    // Revocation info
    public bool Revoked { get; set; }
    public DateTime? RevokedAt { get; set; }
    public string? RevokedBy { get; set; }
    public string? RevocationReason { get; set; }
}

