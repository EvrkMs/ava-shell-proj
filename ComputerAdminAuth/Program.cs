// Program.cs
using ComputerAdminAuth.Data.Context;
using ComputerAdminAuth.Extensions;
using ComputerAdminAuth.Seeders;
using Duende.IdentityServer.EntityFramework.DbContexts;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
var services = builder.Services;
var cfg = builder.Configuration;

/* ---------- Cookie + Bearer ---------- */
services.ConfigureApplicationCookie(o =>
{
    o.LoginPath = "/Account/Login";
    o.LogoutPath = "/Account/Logout";
});

/* ---------- наши сервисы (Identity + IdentityServer + EF-stores) ---------- */
services.AddUserServices(cfg);

services.AddRazorPages();
services.AddControllers();           // api/telegram/*
services.AddAntiforgery();
services.AddHsts(o => { o.IncludeSubDomains = true; o.Preload = true; });

builder.WebHost.UseKestrel(o =>
{
    o.ListenLocalhost(5001, l => l.UseHttps());
});

var app = builder.Build();

/* ---------- Middleware pipeline ---------- */
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});
app.UseCookiePolicy(new CookiePolicyOptions
{
    MinimumSameSitePolicy = SameSiteMode.None,
    Secure = CookieSecurePolicy.Always
});

app.UseHttpsRedirection();
app.UseHsts();

app.UseRouting();

app.UseAuthentication();            // ② JWT / Cookie
app.UseAuthorization();

app.UseIdentityServer();            // ③ endpoints /.well-known, /connect/*
app.MapControllers();
app.MapRazorPages();

/* ---------- миграции + сиды ---------- */
using (var scope = app.Services.CreateScope())
{
    var sp = scope.ServiceProvider;
    await sp.GetRequiredService<AppDbContext>().Database.MigrateAsync();
    await sp.GetRequiredService<ConfigurationDbContext>().Database.MigrateAsync();
    await sp.GetRequiredService<PersistedGrantDbContext>().Database.MigrateAsync();

    await IdentityServerSeeder.SeedAsync(sp);          // клиенты / скоупы / ресурсы
    await UserEntityDefaultSeeder.SeedAsync(sp);       // ваш дефолт-пользователь
    await RoleSeeder.SeedAsync(sp);                    // роли
}

app.Run();
