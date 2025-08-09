using System.Net;
using Auth.Application.UseCases;
using Auth.Host.Pages.Account.Telegram;
using Auth.Infrastructure;
using Auth.Infrastructure.Seeder;
using Auth.Shared.Contracts;
using Duende.IdentityModel;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);
var services = builder.Services;
var cfg = builder.Configuration;

builder.Logging.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Warning);

// --- Application + Infrastructure ---
services.AddApplication();
services.AddInfrastructure(cfg);
// --- Razor Pages и API контроллеры ---
services.AddRazorPages();
services.AddControllers();

// --- JWT Bearer для API ---
services.AddAuthentication()                 // <— без override defaults
    .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, o =>
    {
        o.Authority = cfg["Jwt:Authority"] ?? "https://auth.ava-kk.ru";
        o.Audience = ApiResources.ComputerClubApi;
        o.RequireHttpsMetadata = true;

        o.MapInboundClaims = false;

        o.TokenValidationParameters = new TokenValidationParameters
        {
            NameClaimType = JwtClaimTypes.Subject, // "sub"
            RoleClaimType = JwtClaimTypes.Role
        };
    });

// --- Авторизация по скоупам ---
services.AddAuthorizationBuilder()
    .AddPolicy("ApiWrite", p => p.RequireClaim("scope", ApiScopes.ApiWrite));

// --- CORS для SPA ---
services.AddCors(o => o.AddPolicy("spa", p => p
    .WithOrigins("https://admin.ava-kk.ru")
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials()
));

// --- Antiforgery / HSTS ---
services.AddAntiforgery(o =>
{
    o.HeaderName = "X-CSRF-TOKEN"; // Можно передавать в AJAX-запросах
});

services.AddHsts(o =>
{
    o.IncludeSubDomains = true;
    o.Preload = true;
    o.MaxAge = TimeSpan.FromDays(1);

    o.ExcludedHosts.Add("localhost");
});

// --- Kestrel ---
builder.WebHost.UseKestrel(o =>
{
    o.ListenAnyIP(5001, l =>
    {
        l.UseHttps();
    });

});

var app = builder.Build();

// --- Forwarded headers (за прокси) ---
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
    KnownProxies = { IPAddress.Parse("192.168.88.91") }
});

app.UseCookiePolicy(new CookiePolicyOptions
{
    MinimumSameSitePolicy = SameSiteMode.None,
    Secure = CookieSecurePolicy.Always
});

// --- Middleware ---
app.UseHttpsRedirection();

app.UseHsts();

app.UseRouting();

app.UseCors("spa");

app.UseAuthentication();       // JWT / Cookies
app.UseIdentityServer();       // /.well-known, /connect/*
app.UseAuthorization();

// --- Endpoints ---
app.MapControllers();
app.MapRazorPages();

// --- Миграции и сиды ---
using (var scope = app.Services.CreateScope())
{
    var sp = scope.ServiceProvider;
    await sp.ApplyMigrationsAndSeedAsync();
}

app.Run();
