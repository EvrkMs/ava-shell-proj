using System.Security.Claims;
using Auth.Application.Interfaces;

namespace Auth.Host.Middleware;

public class SessionRevocationMiddleware
{
    private readonly RequestDelegate _next;

    public SessionRevocationMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ISessionService sessions)
    {
        // Only enforce for API routes where bearer tokens are expected
        if (context.Request.Path.StartsWithSegments("/api") &&
            context.User?.Identity?.IsAuthenticated == true)
        {
            var sid = context.User.FindFirst("sid")?.Value;
            if (!string.IsNullOrEmpty(sid))
            {
                var active = await sessions.IsActiveAsync(sid);
                if (!active)
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsync("{\"error\":\"session_revoked\"}");
                    return;
                }
            }
        }

        await _next(context);
    }
}

public static class SessionRevocationMiddlewareExtensions
{
    public static IApplicationBuilder UseSessionRevocationValidation(this IApplicationBuilder app)
        => app.UseMiddleware<SessionRevocationMiddleware>();
}

