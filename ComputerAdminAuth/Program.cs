using ComputerAdminAuth.Data.Context;
using ComputerAdminAuth.Extensions;
using ComputerAdminAuth.Seeders;
using Duende.IdentityServer.EntityFramework.DbContexts;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login"; // путь до твоей Razor Page
    options.LogoutPath = "/Account/logout";
});

builder.Services.AddUserServices(builder.Configuration);
builder.Logging.AddConsole();
builder.Services.AddRazorPages();
builder.Services.AddAntiforgery();
builder.Services.AddHsts(options =>
{
    options.IncludeSubDomains = false;
    options.Preload = true;
});
builder.WebHost.UseKestrel(opt =>
{
    opt.ListenLocalhost(5001, listen =>
    {
        listen.UseHttps();
    });
});

var app = builder.Build();

app.UseForwardedHeaders();

app.UsePathBase("/auth");

app.UseHttpsRedirection();
app.UseHsts();

app.UseAntiforgery();

app.UseRouting();
app.UseAuthentication();   // обязательно для Identity
app.UseAuthorization();    // обязательно для Razor Pages

app.UseIdentityServer();   // уже есть у тебя

app.MapDefaultControllerRoute(); // если используешь контроллеры
app.MapRazorPages();

app.UseStaticFiles();

using (var scope = app.Services.CreateScope())
{
    var provider = scope.ServiceProvider;
    await provider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
    //await provider.GetRequiredService<PersistedGrantDbContext>().Database.MigrateAsync();
    //await provider.GetRequiredService<ConfigurationDbContext>().Database.MigrateAsync();

    //await IdentityServerSeeder.SeedAsync(provider);
    //await IdentityServerSeeder.SeedClientsAsync(provider);
    await UserEntityDefaultSeeder.SeedAsync(provider);
    await RoleSeeder.SeedAsync(provider);
}

app.Run();