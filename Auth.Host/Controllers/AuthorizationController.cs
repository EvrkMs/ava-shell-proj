// Auth.Host\Controllers\AuthorizationController.cs
using System.Security.Claims;
using Auth.Domain.Entities;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Auth.Host.Controllers;

[ApiController]
public class AuthorizationController : ControllerBase
{
    private readonly IOpenIddictApplicationManager _applicationManager;
    private readonly IOpenIddictAuthorizationManager _authorizationManager;
    private readonly IOpenIddictScopeManager _scopeManager;
    private readonly SignInManager<UserEntity> _signInManager;
    private readonly UserManager<UserEntity> _userManager;

    public AuthorizationController(
        IOpenIddictApplicationManager applicationManager,
        IOpenIddictAuthorizationManager authorizationManager,
        IOpenIddictScopeManager scopeManager,
        SignInManager<UserEntity> signInManager,
        UserManager<UserEntity> userManager)
    {
        _applicationManager = applicationManager;
        _authorizationManager = authorizationManager;
        _scopeManager = scopeManager;
        _signInManager = signInManager;
        _userManager = userManager;
    }

    [HttpGet("~/connect/authorize")]
    [HttpPost("~/connect/authorize")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Authorize()
    {
        var request = HttpContext.GetOpenIddictServerRequest() ??
            throw new InvalidOperationException("The OpenID Connect request cannot be retrieved.");

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
                        [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] =
                            "The user is not logged in."
                    }));
            }

            // Сохраняем параметры запроса для возврата после логина
            var parameters = Request.HasFormContentType ?
                [.. Request.Form] :
                Request.Query.ToList();

            return Challenge(
                authenticationSchemes: IdentityConstants.ApplicationScheme,
                properties: new AuthenticationProperties
                {
                    RedirectUri = Request.PathBase + Request.Path + QueryString.Create(parameters)
                });
        }

        // Получаем пользователя
        var userId = _userManager.GetUserId(result.Principal);

        // 2) fallback: иногда в cookie есть только sub (маловероятно, но вдруг)
        if (string.IsNullOrEmpty(userId))
            userId = result.Principal.FindFirstValue(OpenIddictConstants.Claims.Subject);

        // 3) на крайняк — попробуем по имени
        if (string.IsNullOrEmpty(userId) && !string.IsNullOrEmpty(result.Principal.Identity?.Name))
        {
            var byName = await _userManager.FindByNameAsync(result.Principal.Identity.Name);
            if (byName is not null) userId = byName.Id.ToString();
        }
        if (string.IsNullOrEmpty(userId))
        {
            // кука «плохая» — просим перелогиниться
            return Challenge(IdentityConstants.ApplicationScheme);
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
        {
            // пользователь удалён/изменён → заставим перелогиниться
            return Challenge(IdentityConstants.ApplicationScheme);
        }
        // Получаем приложение
        var application = await _applicationManager.FindByClientIdAsync(request.ClientId!) ??
            throw new InvalidOperationException("Details concerning the calling client application cannot be found.");

        // Получаем постоянную авторизацию, если есть
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

        // Автоматически авторизуем, если есть постоянная авторизация или consent_type=implicit
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
                var principal = await CreateUserPrincipalAsync(user, request);

                // Автоматически создаём постоянную авторизацию для избежания повторных запросов consent
                var authorization = authorizations.LastOrDefault();
                authorization ??= await _authorizationManager.CreateAsync(
                        principal: principal,
                        subject: user.Id.ToString(),
                        client: await _applicationManager.GetIdAsync(application),
                        type: AuthorizationTypes.Permanent,
                        scopes: principal.GetScopes());

                principal.SetAuthorizationId(await _authorizationManager.GetIdAsync(authorization));

                return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);

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
                // В реальном приложении здесь должна быть страница согласия (consent)
                // Для упрощения автоматически даём согласие
                var consentPrincipal = await CreateUserPrincipalAsync(user, request);

                // Создаём постоянную авторизацию
                var auth = await _authorizationManager.CreateAsync(
                    principal: consentPrincipal,
                    subject: user.Id.ToString(),
                    client: await _applicationManager.GetIdAsync(application),
                    type: AuthorizationTypes.Permanent,
                    scopes: consentPrincipal.GetScopes());

                consentPrincipal.SetAuthorizationId(await _authorizationManager.GetIdAsync(auth));

                return SignIn(consentPrincipal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }
    }

    [HttpPost("~/connect/token")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Exchange()
    {
        var request = HttpContext.GetOpenIddictServerRequest() ??
            throw new InvalidOperationException("The OpenID Connect request cannot be retrieved.");

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
                        [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] =
                            "The token is no longer valid."
                    }));
            }

            // Получаем профиль пользователя
            var user = await _userManager.FindByIdAsync(result.Principal.GetClaim(Claims.Subject));
            if (user is null)
            {
                return Forbid(
                    authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                    properties: new AuthenticationProperties(new Dictionary<string, string?>
                    {
                        [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.InvalidGrant,
                        [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] =
                            "The token is no longer valid."
                    }));
            }

            // Проверяем, что аккаунт активен
            if (!user.IsActive)
            {
                return Forbid(
                    authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                    properties: new AuthenticationProperties(new Dictionary<string, string?>
                    {
                        [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.InvalidGrant,
                        [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] =
                            "The user is no longer allowed to sign in."
                    }));
            }

            var principal = await CreateUserPrincipalAsync(user, request);

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

        return Ok(claims);
    }

    [HttpGet("~/connect/logout")]
    [HttpPost("~/connect/logout")]
    [IgnoreAntiforgeryToken] // Важно для OIDC logout
    public async Task<IActionResult> Logout()
    {
        var request = HttpContext.GetOpenIddictServerRequest();

        // Выходим из Identity
        await _signInManager.SignOutAsync();

        // Если есть post_logout_redirect_uri, возвращаем SignOutResult
        if (!string.IsNullOrEmpty(request?.PostLogoutRedirectUri))
        {
            return SignOut(
                authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                properties: new AuthenticationProperties
                {
                    RedirectUri = request.PostLogoutRedirectUri
                });
        }

        // Иначе редирект на домашнюю страницу
        return SignOut(
            authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
            properties: new AuthenticationProperties
            {
                RedirectUri = "/"
            });
    }

    private async Task<ClaimsPrincipal> CreateUserPrincipalAsync(UserEntity user, OpenIddictRequest request)
    {
        var principal = await _signInManager.CreateUserPrincipalAsync(user);

        // ВАЖНО: Добавляем обязательный subject claim
        if (principal.Identity is ClaimsIdentity identity)
        {
            // Удаляем существующий subject claim если есть
            var existingSubjectClaim = identity.FindFirst(Claims.Subject);
            if (existingSubjectClaim != null)
            {
                identity.RemoveClaim(existingSubjectClaim);
            }

            // Добавляем правильный subject claim
            identity.AddClaim(new Claim(Claims.Subject, user.Id.ToString()));

            // Также добавляем name claim если его нет
            if (!identity.HasClaim(claim => claim.Type == Claims.Name))
            {
                identity.AddClaim(new Claim(Claims.Name, user.UserName ?? user.Id.ToString()));
            }
        }

        principal.SetScopes(request.GetScopes());

        // Собираем ресурсы асинхронно
        var resources = new List<string>();
        await foreach (var resource in _scopeManager.ListResourcesAsync(principal.GetScopes()))
        {
            resources.Add(resource);
        }
        principal.SetResources(resources);

        // Устанавливаем destinations для claims
        foreach (var claim in principal.Claims)
        {
            claim.SetDestinations(GetDestinations(claim, principal));
        }

        return principal;
    }
    private static IEnumerable<string> GetDestinations(Claim claim, ClaimsPrincipal principal)
    {
        switch (claim.Type)
        {
            case Claims.Subject:
                yield return Destinations.AccessToken;
                yield return Destinations.IdentityToken;
                yield break;

            case Claims.Name:
                yield return Destinations.AccessToken;
                if (principal.HasScope(Scopes.Profile))
                    yield return Destinations.IdentityToken;
                yield break;

            case Claims.Email:
                yield return Destinations.AccessToken;
                if (principal.HasScope(Scopes.Email))
                    yield return Destinations.IdentityToken;
                yield break;

            case Claims.Role:
                yield return Destinations.AccessToken;
                if (principal.HasScope(Scopes.Roles))
                    yield return Destinations.IdentityToken;
                yield break;

            case "AspNet.Identity.SecurityStamp":
                // Никогда не включаем SecurityStamp в токены
                yield break;

            default:
                yield return Destinations.AccessToken;
                yield break;
        }
    }
}