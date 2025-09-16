using Auth.Domain.Entities;
using Auth.EntityFramework.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Auth.Infrastructure.Seeder;

public static class DatabaseSeeder
{
    public static async Task ApplyMigrationsAndSeedAsync(this IServiceProvider sp)
    {
        // Миграции для всех контекстов
        using var scope = sp.CreateScope();
        var provider = scope.ServiceProvider;

        await provider.GetRequiredService<AppDbContext>().Database.MigrateAsync();

        // Сидеры
        await SeedRolesAsync(provider);
        await SeedDefaultUserAsync(provider);
        await OpenIddictSeeder.SeedAsync(provider);
    }

    private static async Task SeedRolesAsync(IServiceProvider sp)
    {
        var roleMgr = sp.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        string[] roles = { "Admin", "Operator", "Root" };

        foreach (var r in roles)
            if (!await roleMgr.RoleExistsAsync(r))
                await roleMgr.CreateAsync(new IdentityRole<Guid>(r));
    }

    private static async Task SeedDefaultUserAsync(IServiceProvider sp)
    {
        var userMgr = sp.GetRequiredService<UserManager<UserEntity>>();
        var existing = await userMgr.FindByNameAsync("root");
        if (existing != null) return;
        // Only seed default root when AUTH_ROOT_PASSWORD is provided via environment
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("AUTH_ROOT_PASSWORD")))
            return;

        var user = new UserEntity
        {
            UserName = "root",
            FullName = "Полный доступ"
        };

        var result = await userMgr.CreateAsync(user, Environment.GetEnvironmentVariable("AUTH_ROOT_PASSWORD")!);
        if (result.Succeeded)
        {
            await userMgr.AddToRoleAsync(user, "Root");
        }
        else
        {
            throw new Exception("Не удалось создать пользователя root: " +
                string.Join(", ", result.Errors.Select(e => e.Description)));
        }
    }
}
