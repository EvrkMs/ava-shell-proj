using Auth.Domain.Entities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Auth.Infrastructure;

public class CustomSignInManager : SignInManager<UserEntity>
{
    public CustomSignInManager(UserManager<UserEntity> userManager,
        IHttpContextAccessor contextAccessor, IUserClaimsPrincipalFactory<UserEntity> claimsFactory,
        IOptions<IdentityOptions> optionsAccessor, ILogger<SignInManager<UserEntity>> logger,
        IAuthenticationSchemeProvider schemes, IUserConfirmation<UserEntity> confirmation)
        : base(userManager, contextAccessor, claimsFactory, optionsAccessor, logger, schemes, confirmation) { }

    public override async Task<SignInResult> PasswordSignInAsync(
        string userName, string password, bool isPersistent, bool lockoutOnFailure)
    {
        var user = await UserManager.FindByNameAsync(userName);
        if (user != null && user.MustChangePassword)
        {
            return SignInResult.NotAllowed; // вместо успешного логина
        }

        return await base.PasswordSignInAsync(userName, password, isPersistent, lockoutOnFailure);
    }
}

