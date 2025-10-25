using Auth.Application.Interfaces;
using Auth.Domain.Entities;
using Auth.Host.Extensions;
using Auth.Host.Services.Support;
using Auth.Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Abstractions;
using System.Security.Claims;

namespace Auth.Host.Services;

/// <summary>
/// Couples browser cookies and OpenIddict grants with our server-side session records.
/// Ensures every interactive login issues an opaque sid reference + a confidential browser secret.
/// </summary>
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

        var cookieValue = http.Request.Cookies[SessionCookie.Name];
        if (!SessionCookie.TryUnpack(cookieValue, out var cookieReference, out var secret) ||
            !string.Equals(cookieReference, sid, StringComparison.Ordinal))
        {
            await ClearSessionAsync(http);
            return await ChallengeAsync(http, request);
        }

        var validation = await _sessions.ValidateBrowserSessionAsync(cookieReference, secret, requireActive: true);
        if (validation is not null)
            return new GuardResult(true, null);

        // Clear sid cookie and sign out the application cookie to force interactive login
        await ClearSessionAsync(http);
        return await ChallengeAsync(http, request);
    }

    public async Task AttachInteractiveSessionAsync(HttpContext http, ClaimsPrincipal principal, UserEntity user, string? clientId)
    {
        var cookieAuth = await http.AuthenticateAsync(IdentityConstants.ApplicationScheme);
        var rememberMe = ResolveRememberMe(cookieAuth);
        var lifetime = rememberMe ? CustomSignInManager.LongSessionLifetime : CustomSignInManager.ShortSessionLifetime;

        // Prefer client IP resolved via forwarded headers; fallback to proxy headers for display only
        var ip = http.GetRealClientIp();
        var ua = http.Request.Headers["User-Agent"].ToString();
        var device = "web";
        var issued = await _sessions.EnsureInteractiveSessionAsync(user.Id, clientId, ip, ua, device, lifetime);
        var sid = issued.ReferenceId;

        var ci = (ClaimsIdentity)principal.Identity!;
        var sidClaim = new Claim("sid", sid);
        ci.AddClaim(sidClaim);
        sidClaim.SetDestinations(
            OpenIddictConstants.Destinations.IdentityToken,
            OpenIddictConstants.Destinations.AccessToken);
        var existingPrincipalPersistence = ci.FindFirst(SessionClaimTypes.Persistence);
        if (existingPrincipalPersistence is not null) ci.RemoveClaim(existingPrincipalPersistence);
        var persistenceClaim = new Claim(SessionClaimTypes.Persistence, rememberMe ? "true" : "false");
        ci.AddClaim(persistenceClaim);
        persistenceClaim.SetDestinations(
            OpenIddictConstants.Destinations.IdentityToken,
            OpenIddictConstants.Destinations.AccessToken);

        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            IsEssential = true,
            Expires = DateTimeOffset.UtcNow.Add(lifetime)
        };
        http.Response.Cookies.Append(SessionCookie.Name, SessionCookie.Pack(sid, issued.BrowserSecret), cookieOptions);

        // Stamp the Identity cookie with sid so silent login is session-bound
        if (cookieAuth?.Succeeded == true && cookieAuth.Principal?.Identity is ClaimsIdentity idCookie)
        {
            var existingSid = idCookie.FindFirst("sid");
            if (existingSid is not null) idCookie.RemoveClaim(existingSid);
            idCookie.AddClaim(new Claim("sid", sid));
            var existingPersistence = idCookie.FindFirst(SessionClaimTypes.Persistence);
            if (existingPersistence is not null) idCookie.RemoveClaim(existingPersistence);
            idCookie.AddClaim(new Claim(SessionClaimTypes.Persistence, rememberMe ? "true" : "false"));
            await http.SignInAsync(IdentityConstants.ApplicationScheme, cookieAuth.Principal, cookieAuth.Properties);
        }
    }

    private async Task ClearSessionAsync(HttpContext http)
    {
        if (http.Request.Cookies.ContainsKey(SessionCookie.Name))
            http.Response.Cookies.Delete(SessionCookie.Name, new CookieOptions { Secure = true, SameSite = SameSiteMode.Lax });

        await _signIn.SignOutAsync();
    }

    private async Task<GuardResult> ChallengeAsync(HttpContext http, OpenIddictRequest request)
    {
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

    private static bool ResolveRememberMe(AuthenticateResult? cookieAuth)
    {
        if (cookieAuth?.Properties?.Items is { } items &&
            items.TryGetValue(CustomSignInManager.RememberMePropertyKey, out var raw) &&
            bool.TryParse(raw, out var rememberMe))
        {
            return rememberMe;
        }

        return cookieAuth?.Properties?.IsPersistent ?? true;
    }
}
