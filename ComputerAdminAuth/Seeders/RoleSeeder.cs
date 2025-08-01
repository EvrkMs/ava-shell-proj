using ComputerAdminAuth.Entities;
using Microsoft.AspNetCore.Identity;

namespace ComputerAdminAuth.Seeders;

public static class RoleSeeder
{
    public static async Task SeedAsync(IServiceProvider sp)
    {
        var roleMgr = sp.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var userMgr = sp.GetRequiredService<UserManager<UserEntity>>();

        string[] roles = { "Admin", "Operator", "Root" };

        foreach (var r in roles)
            if (!await roleMgr.RoleExistsAsync(r))
                await roleMgr.CreateAsync(new IdentityRole<Guid>(r));

        // назначаем root‑пользователю роль Admin
        var root = await userMgr.FindByNameAsync("root");
        if (root is not null && !await userMgr.IsInRoleAsync(root, "Root"))
            await userMgr.AddToRoleAsync(root, "Root");
    }
}
