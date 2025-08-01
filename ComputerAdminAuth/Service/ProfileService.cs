using System.Security.Claims;
using ComputerAdminAuth.Entities;
using Duende.IdentityModel;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Microsoft.AspNetCore.Identity;

namespace ComputerAdminAuth.Service;

public class ProfileService(UserManager<UserEntity> users) : IProfileService
{
    private readonly UserManager<UserEntity> _users = users;

    public async Task GetProfileDataAsync(ProfileDataRequestContext ctx)
    {
        var user = await _users.GetUserAsync(ctx.Subject);

        // 1️⃣  Формируем отображаемое имя
        var displayName = string.IsNullOrWhiteSpace(user.FullName)
                        ? user.UserName                 // fallback
                        : user.FullName;

        // 2️⃣  Добавляем claim "name"
        ctx.IssuedClaims.Add(
            new Claim(JwtClaimTypes.Name, displayName));   // эквивалент "name"

        // 3️⃣  Добавляем роли
        var roles = await _users.GetRolesAsync(user);
        ctx.IssuedClaims.AddRange(
            roles.Select(r => new Claim(JwtClaimTypes.Role, r)));

        // 4️⃣  Опционально: username как preferred_username
        ctx.IssuedClaims.Add(
            new Claim(JwtClaimTypes.PreferredUserName, user.UserName));
    }

    public async Task IsActiveAsync(IsActiveContext ctx)
        => ctx.IsActive = (await _users.GetUserAsync(ctx.Subject))?.IsActive ?? false;
}