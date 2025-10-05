using Auth.Application.Interfaces;
using Auth.Domain.Entities;
using Auth.Host.Extensions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Abstractions;
using System.Security.Claims;

namespace Auth.Host.Services;

public sealed class SessionBindingService
{
    private readonly ISessionService _sessions;
    private readonly SignInManager<UserEntity> _signIn;

    public SessionBindingService(ISessionService sessions, SignInManager<UserEntity> signIn)
    {
        _sessions = sessions;
        _signIn = signIn;
    }

    public sealed record GuardResult(bool Ok, IActionResult? Action);

    public async Task<GuardResult> EnforceCookieSessionOrChallengeAsync(
        HttpContext http,
        OpenIddictRequest request)
    {
        var principal = (await http.AuthenticateAsync(IdentityConstants.ApplicationScheme)).Principal;
        var sid = principal?.FindFirst("sid")?.Value;

        // If cookie not bound to sid yet, allow and a new interactive session will be created later.
        if (string.IsNullOrEmpty(sid))
            return new GuardResult(true, null);

        var active = await _sessions.IsActiveAsync(sid);
        if (active) return new GuardResult(true, null);

        // Clear sid cookie and sign out the application cookie to force interactive login
        if (http.Request.Cookies.ContainsKey("sid"))
            http.Response.Cookies.Delete("sid", new CookieOptions { Secure = true, SameSite = SameSiteMode.Lax });

        await _signIn.SignOutAsync();

        if (request.Prompt == "none")
        {
            var props = new AuthenticationProperties(new Dictionary<string, string?>
            {
                [OpenIddict.Server.AspNetCore.OpenIddictServerAspNetCoreConstants.Properties.Error] = OpenIddictConstants.Errors.LoginRequired,
                [OpenIddict.Server.AspNetCore.OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = "The user must re-authenticate."
            });
            return new GuardResult(false,
                new ForbidResult(new[] { OpenIddict.Server.AspNetCore.OpenIddictServerAspNetCoreDefaults.AuthenticationScheme }, props));
        }

        var parameters = http.Request.HasFormContentType ? [.. http.Request.Form] : http.Request.Query.ToList();
        var chProps = new AuthenticationProperties
        {
            RedirectUri = http.Request.PathBase + http.Request.Path + QueryString.Create(parameters)
        };
        return new GuardResult(false, new ChallengeResult(new[] { IdentityConstants.ApplicationScheme }, chProps));
    }

    public async Task AttachInteractiveSessionAsync(HttpContext http, ClaimsPrincipal principal, UserEntity user, string? clientId)
    {
        // Prefer client IP resolved via forwarded headers; fallback to proxy headers for display only
        var ip = http.GetRealClientIp();
        var ua = http.Request.Headers["User-Agent"].ToString();
        var device = "web";
        var sid = await _sessions.EnsureInteractiveSessionAsync(user.Id, clientId, ip, ua, device, TimeSpan.FromDays(30));

        var ci = (ClaimsIdentity)principal.Identity!;
        var sidClaim = new Claim("sid", sid);
        ci.AddClaim(sidClaim);
        sidClaim.SetDestinations(
            OpenIddictConstants.Destinations.IdentityToken,
            OpenIddictConstants.Destinations.AccessToken);

        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            IsEssential = true,
            Expires = DateTimeOffset.UtcNow.AddDays(30)
        };
        http.Response.Cookies.Append("sid", sid, cookieOptions);

        // Stamp the Identity cookie with sid so silent login is session-bound
        var cookieAuth = await http.AuthenticateAsync(IdentityConstants.ApplicationScheme);
        if (cookieAuth?.Succeeded == true && cookieAuth.Principal?.Identity is ClaimsIdentity idCookie)
        {
            var existingSid = idCookie.FindFirst("sid");
            if (existingSid is not null) idCookie.RemoveClaim(existingSid);
            idCookie.AddClaim(new Claim("sid", sid));
            await http.SignInAsync(IdentityConstants.ApplicationScheme, cookieAuth.Principal);
        }
    }
}
