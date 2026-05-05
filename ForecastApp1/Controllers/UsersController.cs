using ForecastApp1.Data;
using ForecastApp1.Data.Entities;
using ForecastApp1.Dtos;
using ForecastApp1.Security;
using ForecastApp1.Services;
using ForecastApp1.Services.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ForecastApp1.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = AppRoles.Admin)]
public class UsersController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IPasswordService _passwords;
    private readonly IAuditService _audit;

    public UsersController(ApplicationDbContext db, IPasswordService passwords, IAuditService audit)
    {
        _db = db;
        _passwords = passwords;
        _audit = audit;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<UserListDto>>> List(CancellationToken ct)
    {
        var users = await _db.Users.AsNoTracking()
            .Where(u => u.DeletedAt == null)
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .OrderBy(u => u.Email)
            .ToListAsync(ct);
        return Ok(users.Select(u => new UserListDto(u.Id, u.Email, u.FullName, u.IsActive, u.UserRoles.Select(r => r.Role.Name).ToArray())).ToList());
    }

    [HttpPost]
    public async Task<ActionResult> Create([FromBody] CreateUserRequest req, CancellationToken ct)
    {
        if (await _db.Users.AnyAsync(u => u.Email == req.Email && u.DeletedAt == null, ct))
            return Conflict(new { message = "Пользователь уже существует" });

        var user = new AppUser
        {
            Email = req.Email.Trim(),
            FullName = req.FullName.Trim(),
            PasswordHash = _passwords.Hash(req.Password),
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);

        foreach (var roleName in req.Roles.Distinct())
        {
            var role = await _db.Roles.FirstOrDefaultAsync(r => r.Name == roleName, ct);
            if (role is null) continue;
            _db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id, AssignedAt = DateTime.UtcNow });
        }
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync("user.create", "user", user.Id.ToString(), payload: new { user.Email }, ct: ct);
        return Ok(new { id = user.Id });
    }

    [HttpPut("{id:long}")]
    public async Task<ActionResult> Update(long id, [FromBody] UpdateUserRequest req, CancellationToken ct)
    {
        var user = await _db.Users.Include(u => u.UserRoles).FirstOrDefaultAsync(u => u.Id == id && u.DeletedAt == null, ct);
        if (user is null) return NotFound();

        if (req.FullName is not null) user.FullName = req.FullName;
        if (req.IsActive.HasValue) user.IsActive = req.IsActive.Value;
        if (!string.IsNullOrEmpty(req.NewPassword)) user.PasswordHash = _passwords.Hash(req.NewPassword);
        user.UpdatedAt = DateTime.UtcNow;

        if (req.Roles is not null)
        {
            _db.UserRoles.RemoveRange(user.UserRoles);
            foreach (var roleName in req.Roles.Distinct())
            {
                var role = await _db.Roles.FirstOrDefaultAsync(r => r.Name == roleName, ct);
                if (role is null) continue;
                _db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id, AssignedAt = DateTime.UtcNow });
            }
        }
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync("user.update", "user", id.ToString(), ct: ct);
        return Ok();
    }

    [HttpDelete("{id:long}")]
    public async Task<ActionResult> Delete(long id, CancellationToken ct)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id && u.DeletedAt == null, ct);
        if (user is null) return NotFound();
        user.DeletedAt = DateTime.UtcNow;
        user.IsActive = false;
        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync("user.soft_delete", "user", id.ToString(), ct: ct);
        return Ok();
    }
}
