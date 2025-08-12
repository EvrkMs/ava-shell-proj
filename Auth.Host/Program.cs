using System.Net;
using Auth.Application.UseCases;
using Auth.Infrastructure;
using Auth.Infrastructure.Seeder;
using Auth.Shared.Contracts;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using OpenIddict.Validation.AspNetCore;

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

services.AddAuthentication(options =>
{
    options.DefaultScheme = "smart";
})
.AddPolicyScheme("smart", "Dynamic scheme", options =>
{
    options.ForwardDefaultSelector = ctx =>
    {
        if (ctx.Request.Path.StartsWithSegments("/api"))
            return OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;

        if (ctx.Request.Headers.ContainsKey("Authorization"))
            return OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;

        return IdentityConstants.ApplicationScheme;
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

app.Use(async (context, next) =>
{
    context.Response.Headers.Add(
        "Content-Security-Policy",
        "frame-ancestors https://*.telegram.org"
    );
    await next();
});

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
