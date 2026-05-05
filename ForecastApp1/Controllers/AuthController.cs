using System.Security.Claims;
using ForecastApp1.Data;
using ForecastApp1.Dtos;
using ForecastApp1.Services.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ForecastApp1.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IPasswordService _passwords;

    public AuthController(ApplicationDbContext db, IPasswordService passwords)
    {
        _db = db;
        _passwords = passwords;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest req, CancellationToken ct)
    {
        var user = await _db.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Email == req.Email && u.DeletedAt == null, ct);
        if (user is null || !user.IsActive) return Unauthorized(new { message = "Неверный логин или пароль" });
        if (!_passwords.Verify(req.Password, user.PasswordHash))
            return Unauthorized(new { message = "Неверный логин или пароль" });

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Email),
            new("uid", user.Id.ToString())
        };
        foreach (var ur in user.UserRoles)
            claims.Add(new Claim(ClaimTypes.Role, ur.Role.Name));

        var id = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(id));
        return Ok(new { email = user.Email, roles = user.UserRoles.Select(r => r.Role.Name) });
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Ok(new { message = "Вы вышли" });
    }

    [HttpGet("me")]
    public IActionResult Me()
    {
        if (User.Identity?.IsAuthenticated != true) return Unauthorized();
        return Ok(new
        {
            email = User.Identity?.Name,
            roles = User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToArray()
        });
    }
}
