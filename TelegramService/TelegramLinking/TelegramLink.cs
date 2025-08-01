using System.ComponentModel.DataAnnotations;

namespace TelegramService.TelegramLinking;

public class TelegramLink
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    public long TelegramId { get; set; }
    public string TelegramName { get; set; } = string.Empty;

    public Guid UserId { get; set; }
    public DateTime BoundAt { get; set; } = DateTime.UtcNow;
}
