// Auth.Host\Controllers\AuthorizationController.cs
using Auth.Application.Interfaces;
using Auth.Domain.Entities;
using Auth.Host.ProfileService; // IOpenIddictProfileService
using Auth.Host.Services;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using System.Security.Claims;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Auth.Host.Controllers;

[ApiController]
public class AuthorizationController : ControllerBase
{
    private readonly IOpenIddictApplicationManager _applicationManager;
    private readonly IOpenIddictAuthorizationManager _authorizationManager;
    private readonly SignInManager<UserEntity> _signInManager;
    private readonly UserManager<UserEntity> _userManager;
    private readonly IOpenIddictProfileService _profile;
    private readonly ISessionService _sessions;
    private readonly SessionBindingService _sessionBinder;

    public AuthorizationController(
        IOpenIddictApplicationManager applicationManager,
        IOpenIddictAuthorizationManager authorizationManager,
        SignInManager<UserEntity> signInManager,
        UserManager<UserEntity> userManager,
        IOpenIddictProfileService profile,
        ISessionService sessions,
        SessionBindingService sessionBinder)
    {
        _applicationManager = applicationManager;
        _authorizationManager = authorizationManager;
        _signInManager = signInManager;
        _userManager = userManager;
        _profile = profile;
        _sessions = sessions;
        _sessionBinder = sessionBinder;
    }

    [HttpGet("~/connect/authorize")]
    [HttpPost("~/connect/authorize")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Authorize()
    {
        var request = HttpContext.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("The OpenID Connect request cannot be retrieved.");

        // If the browser has a 'sid' cookie that was revoked, prevent silent re-login by clearing cookies
        if (Request.Cookies.TryGetValue("sid", out var cookieSid) && !string.IsNullOrWhiteSpace(cookieSid))
        {
            var stillActive = await _sessions.IsActiveAsync(cookieSid);
            if (!stillActive)
            {
                Response.Cookies.Delete("sid", new CookieOptions { Secure = true, SameSite = SameSiteMode.Lax });
                await _signInManager.SignOutAsync();
            }
        }

        // Если пользователь не залогинен - отправляем на страницу логина
        var result = await HttpContext.AuthenticateAsync(IdentityConstants.ApplicationScheme);
        if (!result.Succeeded)
        {
            // Если prompt=none, возвращаем ошибку без редиректа
            if (request.Prompt == "none")
            {
                return Forbid(
                    authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                    properties: new AuthenticationProperties(new Dictionary<string, string?>
                    {
                        [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.LoginRequired,
                        [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = "The user is not logged in."
                    }));
            }

            // Сохраняем параметры запроса для возврата после логина
            var parameters = Request.HasFormContentType ? [.. Request.Form] : Request.Query.ToList();

            return Challenge(
                authenticationSchemes: IdentityConstants.ApplicationScheme,
                properties: new AuthenticationProperties
                {
                    RedirectUri = Request.PathBase + Request.Path + QueryString.Create(parameters)
                });
        }

        // Получаем пользователя
        // Silent-login guard via service
        var guard = await _sessionBinder.EnforceCookieSessionOrChallengeAsync(HttpContext, request);
        if (!guard.Ok) return guard.Action!;

        var userId = _userManager.GetUserId(result.Principal);
        if (string.IsNullOrEmpty(userId))
            userId = result.Principal.FindFirstValue(OpenIddictConstants.Claims.Subject);

        if (string.IsNullOrEmpty(userId) && !string.IsNullOrEmpty(result.Principal.Identity?.Name))
        {
            var byName = await _userManager.FindByNameAsync(result.Principal.Identity!.Name!);
            if (byName is not null) userId = byName.Id.ToString();
        }

        if (string.IsNullOrEmpty(userId))
            return Challenge(IdentityConstants.ApplicationScheme); // кука «плохая» — просим перелогиниться

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
            return Challenge(IdentityConstants.ApplicationScheme); // пользователь удалён/изменён

        // Клиентское приложение
        var application = await _applicationManager.FindByClientIdAsync(request.ClientId!)
            ?? throw new InvalidOperationException("Details concerning the calling client application cannot be found.");

        // Постоянные авторизации (если есть)
        var authorizations = new List<object>();
        await foreach (var authorization in _authorizationManager.FindAsync(
            subject: user.Id.ToString(),
            client: await _applicationManager.GetIdAsync(application),
            status: Statuses.Valid,
            type: AuthorizationTypes.Permanent,
            scopes: request.GetScopes()))
        {
            authorizations.Add(authorization);
        }

        var consentType = await _applicationManager.GetConsentTypeAsync(application);

        switch (consentType)
        {
            case ConsentTypes.External when !authorizations.Any():
                return Forbid(
                    authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                    properties: new AuthenticationProperties(new Dictionary<string, string?>
                    {
                        [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.ConsentRequired,
                        [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] =
                            "The logged in user is not allowed to access this client application."
                    }));

            case ConsentTypes.Implicit:
            case ConsentTypes.External when authorizations.Any():
            case ConsentTypes.Explicit when authorizations.Any() && request.Prompt != "consent":
                {
                    var principal = await _profile.CreateAsync(user, request);

                    // Create/attach server-side session (sid)
                    await _sessionBinder.AttachInteractiveSessionAsync(HttpContext, principal, user, request.ClientId);

                    // Создаём постоянную авторизацию при необходимости
                    var authorization = authorizations.LastOrDefault();
                    authorization ??= await _authorizationManager.CreateAsync(
                        principal: principal,
                        subject: user.Id.ToString(),
                        client: await _applicationManager.GetIdAsync(application),
                        type: AuthorizationTypes.Permanent,
                        scopes: principal.GetScopes());

                    var authId = await _authorizationManager.GetIdAsync(authorization);
                    // Ensure isolation: create a distinct authorization per interactive session
                    var isolatedAuthorization = await _authorizationManager.CreateAsync(
                        principal: principal,
                        subject: user.Id.ToString(),
                        client: await _applicationManager.GetIdAsync(application),
                        type: AuthorizationTypes.Permanent,
                        scopes: principal.GetScopes());
                    authId = await _authorizationManager.GetIdAsync(isolatedAuthorization);
                    principal.SetAuthorizationId(authId);
                    // Link authorization to current DB session for cascade token revocation
                    var sidVal = principal.GetClaim("sid");
                    if (!string.IsNullOrEmpty(sidVal))
                        await _sessions.LinkAuthorizationAsync(sidVal, authId);

                    return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
                }

            case ConsentTypes.Explicit when request.Prompt == "none":
            case ConsentTypes.Systematic when request.Prompt == "none":
                return Forbid(
                    authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                    properties: new AuthenticationProperties(new Dictionary<string, string?>
                    {
                        [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.ConsentRequired,
                        [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] =
                            "Interactive user consent is required."
                    }));

            default:
                {
                    // (Упрощённо) автоматически даём согласие
                    var consentPrincipal = await _profile.CreateAsync(user, request);

                    await _sessionBinder.AttachInteractiveSessionAsync(HttpContext, consentPrincipal, user, request.ClientId);

                    var auth = await _authorizationManager.CreateAsync(
                        principal: consentPrincipal,
                        subject: user.Id.ToString(),
                        client: await _applicationManager.GetIdAsync(application),
                        type: AuthorizationTypes.Permanent,
                        scopes: consentPrincipal.GetScopes());

                    var authId = await _authorizationManager.GetIdAsync(auth);
                    consentPrincipal.SetAuthorizationId(authId);
                    // Link the current sid to the authorization for precise revocation later
                    var sidVal = consentPrincipal.GetClaim("sid");
                    if (!string.IsNullOrEmpty(sidVal))
                        await _sessions.LinkAuthorizationAsync(sidVal, authId);

                    return SignIn(consentPrincipal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
                }
        }
    }

    [HttpPost("~/connect/token")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Exchange()
    {
        var request = HttpContext.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("The OpenID Connect request cannot be retrieved.");

        if (request.IsAuthorizationCodeGrantType() || request.IsRefreshTokenGrantType())
        {
            // Извлекаем principal из authorization code/refresh token
            var result = await HttpContext.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            if (!result.Succeeded)
            {
                return Forbid(
                    authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                    properties: new AuthenticationProperties(new Dictionary<string, string?>
                    {
                        [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.InvalidGrant,
                        [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = "The token is no longer valid."
                    }));
            }

            // Пользователь
            var user = await _userManager.FindByIdAsync(result.Principal!.GetClaim(Claims.Subject));
            if (user is null || !user.IsActive)
            {
                return Forbid(
                    authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                    properties: new AuthenticationProperties(new Dictionary<string, string?>
                    {
                        [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.InvalidGrant,
                        [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] =
                            user is null ? "The token is no longer valid." : "The user is no longer allowed to sign in."
                    }));
            }

            var principal = await _profile.CreateAsync(user, request);

            // Carry over session id from the original grant (if any)
            var sid = result.Principal!.GetClaim("sid");
            // Also carry over authorization id from the original grant (bind tokens to same auth)
            var originalAuthId = result.Principal!.GetAuthorizationId();
            if (!string.IsNullOrEmpty(originalAuthId))
            {
                principal.SetAuthorizationId(originalAuthId);
                // Ensure session<->authorization link exists for reliable revocation
                if (!string.IsNullOrEmpty(sid))
                    await _sessions.LinkAuthorizationAsync(sid, originalAuthId);
            }
            if (string.IsNullOrEmpty(sid))
            {
                // For refresh_token grant: session-bound is mandatory (block legacy RTs without sid)
                if (request.IsRefreshTokenGrantType())
                {
                    return Forbid(
                        authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                        properties: new AuthenticationProperties(new Dictionary<string, string?>
                        {
                            [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.InvalidGrant,
                            [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = "The session context is missing."
                        }));
                }

                // For authorization_code grant: establish a new interactive session and stamp sid
                await _sessionBinder.AttachInteractiveSessionAsync(HttpContext, principal, user, request.ClientId);
            }
            else
            {
                var ci = (ClaimsIdentity)principal.Identity!;
                var sidClaim = new Claim("sid", sid);
                ci.AddClaim(sidClaim);
                // Ensure "sid" is emitted into id_token/access_token
                sidClaim.SetDestinations(
                    OpenIddictConstants.Destinations.IdentityToken,
                    OpenIddictConstants.Destinations.AccessToken);
                await _sessions.TouchAsync(sid);
                var active = await _sessions.IsActiveAsync(sid);
                if (!active)
                {
                    return Forbid(
                        authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                        properties: new AuthenticationProperties(new Dictionary<string, string?>
                        {
                            [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.InvalidGrant,
                            [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = "The session has been revoked."
                        }));
                }

                // Refresh sid cookie lifetime on token exchange
                var cookieOptions = new CookieOptions
                {
                    HttpOnly = true,
                    Secure = true,
                    SameSite = SameSiteMode.Lax,
                    IsEssential = true,
                    Expires = DateTimeOffset.UtcNow.AddDays(30)
                };
                Response.Cookies.Append("sid", sid, cookieOptions);
            }

            return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        throw new InvalidOperationException("The specified grant type is not supported.");
    }

    [HttpGet("~/connect/userinfo")]
    [Authorize(AuthenticationSchemes = OpenIddictServerAspNetCoreDefaults.AuthenticationScheme)]
    public async Task<IActionResult> UserInfo()
    {
        var claimsPrincipal = (await HttpContext.AuthenticateAsync(
            OpenIddictServerAspNetCoreDefaults.AuthenticationScheme)).Principal;

        // Enforce session-bound access: deny if the session referenced by 'sid' is revoked
        var sid = claimsPrincipal!.GetClaim("sid");
        if (!string.IsNullOrEmpty(sid))
        {
            var active = await _sessions.IsActiveAsync(sid);
            if (!active)
            {
                return Forbid(
                    authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                    properties: new AuthenticationProperties(new Dictionary<string, string?>
                    {
                        [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.InvalidToken,
                        [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = "The session has been revoked."
                    }));
            }
        }

        var user = await _userManager.FindByIdAsync(claimsPrincipal!.GetClaim(Claims.Subject));
        if (user is null)
        {
            return Challenge(
                authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                properties: new AuthenticationProperties(new Dictionary<string, string?>
                {
                    [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.InvalidToken,
                    [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] =
                        "The specified access token is bound to an account that no longer exists."
                }));
        }

        var claims = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            [Claims.Subject] = user.Id.ToString()
        };

        if (claimsPrincipal.HasScope(Scopes.Profile))
        {
            claims[Claims.Name] = user.UserName ?? "";
            claims[Claims.PreferredUsername] = user.UserName ?? "";
            claims["full_name"] = user.FullName ?? "";
        }

        if (claimsPrincipal.HasScope(Scopes.Email))
        {
            claims[Claims.Email] = user.Email ?? "";
            claims[Claims.EmailVerified] = user.EmailConfirmed;
        }

        if (claimsPrincipal.HasScope(Scopes.Phone))
        {
            claims[Claims.PhoneNumber] = user.PhoneNumber ?? "";
            claims[Claims.PhoneNumberVerified] = user.PhoneNumberConfirmed;
        }

        // Also include roles so SPA can render privileges from userinfo
        var roles = await _userManager.GetRolesAsync(user);
        if (roles is not null && roles.Count > 0)
        {
            claims[Claims.Role] = roles.ToArray();
            claims[ClaimTypes.Role] = roles.ToArray();
            claims["roles"] = roles.ToArray();
        }

        return Ok(claims);
    }

    [HttpGet("~/connect/logout")]
    [HttpPost("~/connect/logout")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Logout()
    {
        var request = HttpContext.GetOpenIddictServerRequest();

        // Try to revoke the DB session referenced by id_token_hint (sid)
        try
        {
            var oidc = await HttpContext.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            var principal = oidc?.Principal;
            var sid = principal?.GetClaim("sid");
            if (string.IsNullOrEmpty(sid))
            {
                // Fallback: try from secure cookie set at sign-in
                if (Request.Cookies.TryGetValue("sid", out var cookieSid) && !string.IsNullOrWhiteSpace(cookieSid))
                {
                    sid = cookieSid;
                }
            }
            if (!string.IsNullOrEmpty(sid))
            {
                var by = principal?.GetClaim(Claims.Subject) ?? User?.FindFirstValue(Claims.Subject) ?? User?.Identity?.Name;
                await _sessions.RevokeAsync(sid!, reason: "logout", by: by);
            }
        }
        catch
        {
            // Best-effort: don't block logout if revocation fails
        }

        // Clean up sid cookie regardless
        if (Request.Cookies.ContainsKey("sid"))
        {
            Response.Cookies.Delete("sid", new CookieOptions { Secure = true, SameSite = SameSiteMode.Lax });
        }

        await _signInManager.SignOutAsync();

        if (!string.IsNullOrEmpty(request?.PostLogoutRedirectUri))
        {
            return SignOut(
                authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                properties: new AuthenticationProperties { RedirectUri = request.PostLogoutRedirectUri });
        }

        return SignOut(
            authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
            properties: new AuthenticationProperties { RedirectUri = "/" });
    }
}



