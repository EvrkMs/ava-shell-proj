using ComputerAdminAuth.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace ComputerAdminAuth.Data.Extension;

public static class DbContextExtension
{
    public static IServiceCollection AddDbConnection(this IServiceCollection services, IConfiguration config)
    {
        var connectionString = config.GetConnectionString("DefaultConnection");

        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(connectionString)); // Или другой UseSqlServer, UseSqlite, и т.д.

        return services;
    }
}
