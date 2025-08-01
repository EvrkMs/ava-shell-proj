using ComputerAdminAuth.Entities;
using Microsoft.AspNetCore.Identity;

namespace ComputerAdminAuth.Seeders;

public class UserEntityDefaultSeeder
{
    public static async Task SeedAsync(IServiceProvider serviceProvider)
    {
        var userManager = serviceProvider.GetRequiredService<UserManager<UserEntity>>();

        var existing = await userManager.FindByNameAsync("root");
        if (existing != null)
            return;

        var userDefault = new UserEntity
        {
            UserName = "root",
            FullName = "Полный доступ"
        };

        var result = await userManager.CreateAsync(userDefault, "Root1234");

        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            throw new Exception($"Не удалось создать пользователя root: {errors}");
        }
    }
}
