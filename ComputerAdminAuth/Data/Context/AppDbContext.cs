using ComputerAdminAuth.Data.Configurations;
using ComputerAdminAuth.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace ComputerAdminAuth.Data.Context;

public class AppDbContext(DbContextOptions<AppDbContext> opt)
    : IdentityDbContext<UserEntity, IdentityRole<Guid>, Guid>(opt)
{
    public DbSet<TelegramEntity> TelegramEntities => Set<TelegramEntity>();
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        modelBuilder.ApplyConfiguration(new TelegramConfiguration());
        modelBuilder.ApplyConfiguration(new UserConfiguration());
    }
}
