using Auth.Domain.Entities;
using Auth.Infrastructure.Data;
using Duende.IdentityServer.EntityFramework.DbContexts;
using Duende.IdentityServer.EntityFramework.Mappers;
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
        await provider.GetRequiredService<ConfigurationDbContext>().Database.MigrateAsync();
        await provider.GetRequiredService<PersistedGrantDbContext>().Database.MigrateAsync();

        // Сидеры
        await SeedRolesAsync(provider);
        await SeedDefaultUserAsync(provider);
        await SeedIdentityServerConfigAsync(provider);
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

        var user = new UserEntity
        {
            UserName = "root",
            FullName = "Полный доступ"
        };

        var result = await userMgr.CreateAsync(user, "Root1234");
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

    private static async Task SeedIdentityServerConfigAsync(IServiceProvider sp)
    {
        var ctx = sp.GetRequiredService<ConfigurationDbContext>();

        if (!ctx.Clients.Any())
        {
            ctx.Clients.Add(IdentityServerSeeder.GetClients().First().ToEntity());
        }
        if (!ctx.IdentityResources.Any())
        {
            ctx.IdentityResources.AddRange(
                IdentityServerSeeder.GetIdentityResources().Select(r => r.ToEntity()));
        }
        if (!ctx.ApiScopes.Any())
        {
            ctx.ApiScopes.AddRange(
                IdentityServerSeeder.GetApiScopes().Select(s => s.ToEntity()));
        }
        if (!ctx.ApiResources.Any())
        {
            ctx.ApiResources.AddRange(
                IdentityServerSeeder.GetApiResources().Select(r => r.ToEntity()));
        }

        await ctx.SaveChangesAsync();
    }
}
