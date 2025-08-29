using System.Net;
using System.Text.Json.Serialization;
using Auth.Application.UseCases;
using Auth.Host.ProfileService;
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

// Application + Infrastructure
services.AddApplication();
services.AddInfrastructure(cfg);

// Razor Pages + API controllers
services.AddRazorPages();
services.AddControllers()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: true));
        o.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
    });

services.AddScoped<IOpenIddictProfileService, OpenIddictProfileService>();

// Smart authentication scheme: OpenIddict validation for API, Cookies otherwise
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

// CORS for SPA
services.AddCors(o => o.AddPolicy("spa", p => p
    .WithOrigins("https://admin.ava-kk.ru")
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials()
));

// Antiforgery / HSTS handled at reverse proxy; set antiforgery header name here
services.AddAntiforgery(o =>
{
    o.HeaderName = "X-CSRF-TOKEN";
});

// Kestrel: listen HTTP internally; HTTPS is terminated by reverse proxy
builder.WebHost.UseKestrel(o =>
{
    o.ListenAnyIP(5001);
});

var app = builder.Build();

// Minimal CSP to restrict embedding from specific origin
app.Use(async (context, next) =>
{
    context.Response.Headers["Content-Security-Policy"] = "frame-ancestors 'self' https://admin.ava-kk.ru";
    await next();
});

// Forwarded headers (X-Forwarded-For/Proto) from reverse proxy
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

// Pipeline
app.UseRouting();
app.UseCors("spa");
app.UseAuthentication();
app.UseAuthorization();

// Endpoints
app.MapControllers();
app.MapRazorPages();

// Migrations + Seed
using (var scope = app.Services.CreateScope())
{
    var sp = scope.ServiceProvider;
    await sp.ApplyMigrationsAndSeedAsync();
}

app.Run();

