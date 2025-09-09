using System.Net;
using System.Diagnostics;
using System.Text.Json.Serialization;
using System.Security.Cryptography.X509Certificates;
using System.IO.Compression;
using System.Linq;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.AspNetCore.ResponseCompression;
using Auth.Application.UseCases;
using Auth.Host.ProfileService;
using Auth.Infrastructure;
using Auth.Infrastructure.Seeder;
using Auth.Shared.Contracts;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using OpenIddict.Validation.AspNetCore;
using Auth.Host.Middleware;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);
var services = builder.Services;
var cfg = builder.Configuration;

builder.Logging.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Warning);
builder.Logging.AddFilter("Npgsql", LogLevel.Warning);
builder.Logging.AddFilter("Microsoft.AspNetCore.Cors", LogLevel.Debug);

// Bind Kestrel to HTTPS on 5001 with an in-memory self-signed certificate
// Kestrel TLS: load shared cert from /tls (or generate and persist)
builder.WebHost.UseKestrel(o =>
{
    o.ListenAnyIP(5001, listen =>
    {
        listen.UseHttps(https =>
        {
            https.ServerCertificate = EphemeralCert.Create();
        });
    });
});
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

// Response compression (Brotli/Gzip) for HTML/JSON
services.AddResponseCompression(o =>
{
    o.EnableForHttps = true;
    o.Providers.Add<BrotliCompressionProvider>();
    o.Providers.Add<GzipCompressionProvider>();
    o.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(new[]
    {
        "application/json",
        "application/problem+json"
    });
});
services.Configure<BrotliCompressionProviderOptions>(o => o.Level = CompressionLevel.Fastest);
services.Configure<GzipCompressionProviderOptions>(o => o.Level = CompressionLevel.Fastest);

// Output caching: cache anonymous GETs for short time
services.AddOutputCache(o =>
{
    o.AddPolicy("AnonRazor", b => b
        .Expire(TimeSpan.FromSeconds(60))
        .SetVaryByQuery("*")
        .SetVaryByHeader("Accept-Encoding")
        .SetVaryByHeader("Cookie"));
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

// CORS for SPA (bearer tokens only; no credentials)
services.AddCors(o => o.AddPolicy("spa", p => p
    .WithOrigins("https://admin.ava-kk.ru")
    .WithHeaders("Authorization", "Content-Type")
    .WithMethods("GET", "POST", "PUT", "DELETE", "OPTIONS")
));

// Antiforgery / HSTS handled at reverse proxy; set antiforgery header name here
services.AddAntiforgery(o =>
{
    o.HeaderName = "X-CSRF-TOKEN";
});

// Basic rate limiting: stricter limits for token endpoints
services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

var app = builder.Build();

// CSP is handled by the sidecar nginx in front of this app.

// Forwarded headers (X-Forwarded-For/Proto) from reverse proxy
var fwd = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
    RequireHeaderSymmetry = false,
    ForwardLimit = null
};
fwd.KnownNetworks.Clear();
fwd.KnownProxies.Clear();
// Configure trusted proxies/networks and allowed hosts from configuration/env
var knownProxiesCsv = cfg["ForwardedHeaders:KnownProxies"];
if (!string.IsNullOrWhiteSpace(knownProxiesCsv))
{
    foreach (var s in knownProxiesCsv.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries))
    {
        if (IPAddress.TryParse(s, out var ip))
            fwd.KnownProxies.Add(ip);
    }
}

var knownNetworksCsv = cfg["ForwardedHeaders:KnownNetworks"];
if (!string.IsNullOrWhiteSpace(knownNetworksCsv))
{
    foreach (var s in knownNetworksCsv.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries))
    {
        var parts = s.Split('/', 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 2 && IPAddress.TryParse(parts[0], out var addr) && int.TryParse(parts[1], out var prefix))
        {
            try { fwd.KnownNetworks.Add(new Microsoft.AspNetCore.HttpOverrides.IPNetwork(addr, prefix)); } catch { }
        }
        else if (IPAddress.TryParse(s, out var singleIp))
        {
            fwd.KnownProxies.Add(singleIp);
        }
    }
}

var allowedHostsCsv = cfg["ForwardedHeaders:AllowedHosts"];
if (!string.IsNullOrWhiteSpace(allowedHostsCsv))
{
    foreach (var h in allowedHostsCsv.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries))
        fwd.AllowedHosts.Add(h);
}

// Allow forwarded headers from any source (trust all proxies/networks).
// This disables the default restriction requiring a known proxy and silences Unknown proxy warnings.
try
{
    fwd.KnownNetworks.Add(new Microsoft.AspNetCore.HttpOverrides.IPNetwork(System.Net.IPAddress.Parse("0.0.0.0"), 0));
    fwd.KnownNetworks.Add(new Microsoft.AspNetCore.HttpOverrides.IPNetwork(System.Net.IPAddress.Parse("::"), 0));
}
catch { }
app.UseForwardedHeaders(fwd);

app.UseCookiePolicy(new CookiePolicyOptions
{
    MinimumSameSitePolicy = SameSiteMode.None,
    Secure = CookieSecurePolicy.Always
});

// Pipeline
// Avoid response compression for OpenID Connect endpoints to prevent
// any proxy/client inconsistencies with Content-Length/body size during token exchange.
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/connect"))
    {
        // Remove accepted encodings so ResponseCompression won't engage.
        context.Request.Headers.Remove("Accept-Encoding");
    }
    await next();
});

app.UseResponseCompression();
app.UseRouting();
// CORS logging middleware (place before UseCors to capture preflight short-circuit)
app.Use(async (context, next) =>
{
    var origin = context.Request.Headers["Origin"].ToString();
    if (!string.IsNullOrEmpty(origin))
    {
        var preflightMethod = context.Request.Headers["Access-Control-Request-Method"].ToString();
        var preflightHeaders = context.Request.Headers["Access-Control-Request-Headers"].ToString();

        context.RequestServices.GetRequiredService<ILoggerFactory>()
            .CreateLogger("CorsLogger")
            .LogInformation(
                "CORS request {Method} {Path} Origin={Origin} ACRM={ACRM} ACRH={ACRH}",
                context.Request.Method,
                context.Request.Path,
                origin,
                preflightMethod,
                preflightHeaders);

        context.Response.OnStarting(state =>
        {
            var http = (HttpContext)state;
            var aco = http.Response.Headers["Access-Control-Allow-Origin"].ToString();
            var acm = http.Response.Headers["Access-Control-Allow-Methods"].ToString();
            var ach = http.Response.Headers["Access-Control-Allow-Headers"].ToString();
            var acc = http.Response.Headers["Access-Control-Allow-Credentials"].ToString();

            http.RequestServices.GetRequiredService<ILoggerFactory>()
                .CreateLogger("CorsLogger")
                .LogInformation(
                    "CORS response {Status} ACO={ACO} ACM={ACM} ACH={ACH} ACC={ACC}",
                    http.Response.StatusCode, aco, acm, ach, acc);
            return Task.CompletedTask;
        }, context);
    }

    await next();
});
app.UseOutputCache();
// Apply global rate limiter with per-path budgets (tighter for /connect/*)
app.UseRateLimiter(new RateLimiterOptions
{
    GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var isConnect = context.Request.Path.StartsWithSegments("/connect");
        var key = (isConnect ? "connect:" : "other:") + ip;
        return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = isConnect ? 10 : 100,
            Window = TimeSpan.FromSeconds(10),
            QueueLimit = 0,
            AutoReplenishment = true
        });
    }),
    RejectionStatusCode = StatusCodes.Status429TooManyRequests
});
// OAuth/OIDC endpoints must not be cached: set headers before response starts
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/connect"))
    {
        context.Response.OnStarting(() =>
        {
            context.Response.Headers["Cache-Control"] = "no-store";
            context.Response.Headers["Pragma"] = "no-cache";
            return Task.CompletedTask;
        });
    }
    await next();
});
app.UseCors("spa");
app.UseAuthentication();
// Reject API calls if the DB session (sid) is revoked
app.UseSessionRevocationValidation();
app.UseAuthorization();

// Log slow requests to spot intermittent stalls
app.Use(async (context, next) =>
{
    var sw = Stopwatch.StartNew();
    try
    {
        await next();
    }
    finally
    {
        sw.Stop();
        if (sw.ElapsedMilliseconds > 1000)
        {
            app.Logger.LogWarning("Slow request {Method} {Path} took {Elapsed} ms, status {Status}",
                context.Request.Method, context.Request.Path, sw.ElapsedMilliseconds, context.Response.StatusCode);
        }
    }
});

// Endpoints
app.MapControllers();
app.MapRazorPages();
app.MapGet("/healthz", () => Results.Ok("ok"));

// Migrations + Seed
using (var scope = app.Services.CreateScope())
{
    var sp = scope.ServiceProvider;
    await sp.ApplyMigrationsAndSeedAsync();
}

app.Run();

// Ephemeral self-signed certificate (no files are created or required)
static class EphemeralCert
{
    public static X509Certificate2 Create()
    {
        using var rsa = System.Security.Cryptography.RSA.Create(2048);
        var req = new CertificateRequest(
            "CN=auth-host",
            rsa,
            System.Security.Cryptography.HashAlgorithmName.SHA256,
            System.Security.Cryptography.RSASignaturePadding.Pkcs1);

        var san = new SubjectAlternativeNameBuilder();
        san.AddDnsName("auth-host");
        san.AddDnsName("localhost");
        san.AddIpAddress(IPAddress.Loopback);
        req.CertificateExtensions.Add(san.Build());
        req.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
        req.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, false));

        var now = DateTimeOffset.UtcNow.AddMinutes(-5);
        var cert = req.CreateSelfSigned(now, now.AddYears(5));
        // Rewrap to ensure Kestrel can access the private key across platforms
        return new X509Certificate2(cert.Export(X509ContentType.Pfx));
    }
}
