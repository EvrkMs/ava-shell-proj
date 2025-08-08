using Auth.Application.UseCases.Telegram;
using Microsoft.Extensions.DependencyInjection;

namespace Auth.Application.UseCases;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<GetMyTelegramQuery>();
        services.AddTransient<BindTelegramCommand>();
        services.AddTransient<UnbindTelegramCommand>();

        return services;
    }
}
