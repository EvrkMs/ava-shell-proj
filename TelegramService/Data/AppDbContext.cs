using Microsoft.EntityFrameworkCore;
using TelegramService.TelegramLinking;

namespace TelegramService.Data;

public class AppDbContext(DbContextOptions<AppDbContext> opt) : DbContext(opt)
{
    public DbSet<TelegramLink> TelegramLinks => Set<TelegramLink>();
}
