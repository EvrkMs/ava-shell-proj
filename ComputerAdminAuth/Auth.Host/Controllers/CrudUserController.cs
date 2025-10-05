using Auth.Domain.Entities;
using Auth.EntityFramework.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Validation.AspNetCore;
using System.ComponentModel.DataAnnotations;

namespace Auth.Host.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize(AuthenticationSchemes = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme, Roles = "Root")]
public class CrudUserController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly UserManager<UserEntity> _userManager;
    private readonly RoleManager<IdentityRole<Guid>> _roleManager;

    public CrudUserController(
        AppDbContext db,
        UserManager<UserEntity> userManager,
        RoleManager<IdentityRole<Guid>> roleManager)
    {
        _db = db;
        _userManager = userManager;
        _roleManager = roleManager;
    }

    // --- GET: /api/cruduser?query=...&status=Active|Inactive
    [HttpGet]
    public async Task<ActionResult<IEnumerable<UserListItemDto>>> GetAll(
        [FromQuery] string? query,
        [FromQuery] UserStatus? status,
        CancellationToken ct = default)
    {
        IQueryable<UserEntity> q = _db.Users.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query))
        {
            var qNorm = query.Trim();
            // Если не Postgres — замени ILike -> Like и сравнивай в lower
            q = q.Where(u =>
                (u.Email != null && EF.Functions.ILike(u.Email, $"%{qNorm}%")) ||
                (u.UserName != null && EF.Functions.ILike(u.UserName, $"%{qNorm}%")) ||
                (u.FullName != null && EF.Functions.ILike(u.FullName, $"%{qNorm}%")) ||
                (u.PhoneNumber != null && EF.Functions.ILike(u.PhoneNumber, $"%{qNorm}%")));
        }

        if (status.HasValue)
            q = q.Where(u => u.Status == status.Value);

        var users = await q.OrderByDescending(u => u.CreatedAt).ToListAsync(ct);
        var userIds = users.Select(u => u.Id).ToArray();

        // Маппинг ролей без N+1
        var userRolePairs = await _db.UserRoles
            .Where(ur => userIds.Contains(ur.UserId))
            .Join(_db.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => new { ur.UserId, r.Name })
            .ToListAsync(ct);

        var rolesByUser = userRolePairs
            .GroupBy(x => x.UserId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Name!).OrderBy(n => n).ToArray());

        var items = users.Select(u => ToDto(u, rolesByUser.GetValueOrDefault(u.Id, Array.Empty<string>()))).ToList();
        return Ok(items);
    }

    // --- GET: /api/cruduser/{id}
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<UserListItemDto>> GetById(Guid id, CancellationToken ct = default)
    {
        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id, ct);
        if (user is null) return NotFound();

        var roles = await _db.UserRoles
            .Where(ur => ur.UserId == id)
            .Join(_db.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => r.Name!)
            .OrderBy(n => n)
            .ToArrayAsync(ct);

        return Ok(ToDto(user, roles));
    }

    // --- GET: /api/cruduser/roles
    // Справочник ролей
    [HttpGet("roles")]
    public async Task<ActionResult<IEnumerable<RoleDto>>> GetRoles(CancellationToken ct = default)
    {
        var roles = await _roleManager.Roles
            .AsNoTracking()
            .OrderBy(r => r.Name)
            .Select(r => new RoleDto(r.Id, r.Name ?? string.Empty))
            .ToListAsync(ct);

        return Ok(roles);
    }

    // --- POST: /api/cruduser
    // Создание: login(UserName), password, fullName, status, roles?
    [HttpPost]
    public async Task<ActionResult<UserListItemDto>> Create([FromBody] CreateUserRequest req, CancellationToken ct = default)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        // Проверим, что все заявленные роли существуют (по именам)
        string[] rolesToAssign = Array.Empty<string>();
        if (req.Roles is { Count: > 0 })
        {
            var requested = req.Roles.Select(r => r.Trim()).Where(r => !string.IsNullOrWhiteSpace(r)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            var existing = await _roleManager.Roles
                .Where(r => requested.Contains(r.Name!))
                .Select(r => r.Name!)
                .ToArrayAsync(ct);

            var missing = requested.Except(existing, StringComparer.OrdinalIgnoreCase).ToArray();
            if (missing.Length > 0)
            {
                ModelState.AddModelError("Roles", $"Не найдены роли: {string.Join(", ", missing)}");
                return ValidationProblem(ModelState);
            }
            rolesToAssign = existing;
        }

        var user = new UserEntity
        {
            Id = Guid.NewGuid(),
            UserName = req.UserName.Trim(),
            FullName = req.FullName.Trim(),
            Status = req.Status ?? UserStatus.Active,
            MustChangePassword = true,
            UpdatedAt = DateTime.UtcNow
        };

        var createRes = await _userManager.CreateAsync(user, req.Password);
        if (!createRes.Succeeded)
        {
            foreach (var err in createRes.Errors)
                ModelState.AddModelError(err.Code, err.Description);
            return ValidationProblem(ModelState);
        }

        if (rolesToAssign.Length > 0)
        {
            var addRolesRes = await _userManager.AddToRolesAsync(user, rolesToAssign);
            if (!addRolesRes.Succeeded)
            {
                foreach (var err in addRolesRes.Errors)
                    ModelState.AddModelError(err.Code, err.Description);
                // При неудаче ролей можно откатить пользователя, если нужно:
                // await _userManager.DeleteAsync(user);
                return ValidationProblem(ModelState);
            }
        }

        // Сразу вернём с ролями
        return CreatedAtAction(nameof(GetById), new { id = user.Id }, ToDto(user, rolesToAssign));
    }

    // --- POST: /api/cruduser/{id}/password
    // Единственное «изменяемое» поле — пароль (админом)
    [HttpPost("{id:guid}/password")]
    public async Task<IActionResult> ChangePassword(Guid id, [FromBody] ChangePasswordRequest req, CancellationToken ct = default)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var user = await _userManager.Users.FirstOrDefaultAsync(u => u.Id == id, ct);
        if (user is null) return NotFound();

        var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);
        var result = await _userManager.ResetPasswordAsync(user, resetToken, req.NewPassword);
        if (!result.Succeeded)
        {
            foreach (var err in result.Errors)
                ModelState.AddModelError(err.Code, err.Description);
            return ValidationProblem(ModelState);
        }

        user.MustChangePassword = req.RequireChangeOnNextLogin ?? true;
        user.UpdatedAt = DateTime.UtcNow;

        await _userManager.UpdateSecurityStampAsync(user);

        var saveRes = await _userManager.UpdateAsync(user);
        if (!saveRes.Succeeded)
        {
            foreach (var err in saveRes.Errors)
                ModelState.AddModelError(err.Code, err.Description);
            return ValidationProblem(ModelState);
        }

        return NoContent();
    }

    private static UserListItemDto ToDto(UserEntity u, IReadOnlyCollection<string> roles) => new()
    {
        Id = u.Id,
        UserName = u.UserName ?? string.Empty,
        Email = u.Email ?? string.Empty,
        FullName = u.FullName,
        PhoneNumber = u.PhoneNumber,
        Status = u.Status,
        IsActive = u.IsActive,
        MustChangePassword = u.MustChangePassword,
        CreatedAt = u.CreatedAt,
        UpdatedAt = u.UpdatedAt,
        Roles = roles.ToArray()
    };

    // Перегрузка на случай старых вызовов
    private static UserListItemDto ToDto(UserEntity u) => ToDto(u, Array.Empty<string>());
}

// ===== DTO / запросы =====

public record RoleDto(Guid Id, string Name);

public record UserListItemDto
{
    public Guid Id { get; init; }
    public string UserName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public string? PhoneNumber { get; init; }
    public UserStatus Status { get; init; }
    public bool IsActive { get; init; }
    public bool MustChangePassword { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public string[] Roles { get; init; } = Array.Empty<string>();
}

public record CreateUserRequest
{
    [Required, MinLength(3)]
    public string UserName { get; init; } = string.Empty;

    [Required, MinLength(6)]
    public string Password { get; init; } = string.Empty;

    [Required, MinLength(2)]
    public string FullName { get; init; } = string.Empty;

    public UserStatus? Status { get; init; } = UserStatus.Active;

    // Имена ролей (например: ["Root","Manager"])
    public List<string>? Roles { get; init; }
}

public record ChangePasswordRequest
{
    [Required, MinLength(6)]
    public string NewPassword { get; init; } = string.Empty;

    public bool? RequireChangeOnNextLogin { get; init; } = true;
}
