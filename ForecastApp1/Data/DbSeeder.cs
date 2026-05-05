using ForecastApp1.Data.Entities;
using ForecastApp1.Security;
using ForecastApp1.Services.Auth;
using Microsoft.EntityFrameworkCore;

namespace ForecastApp1.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(ApplicationDbContext db, IPasswordService passwords, CancellationToken ct = default)
    {
        if (!await db.Roles.AnyAsync(ct))
        {
            db.Roles.AddRange(
                new Role { Name = AppRoles.Admin, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
                new Role { Name = AppRoles.Accountant, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
            await db.SaveChangesAsync(ct);
        }

        if (!await db.Users.AnyAsync(u => u.Email == "admin@local", ct))
        {
            var adminRole = await db.Roles.SingleAsync(r => r.Name == AppRoles.Admin, ct);
            var user = new AppUser
            {
                Email = "admin@local",
                FullName = "Администратор",
                PasswordHash = passwords.Hash("ChangeMe!1"),
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            db.Users.Add(user);
            await db.SaveChangesAsync(ct);
            db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = adminRole.Id, AssignedAt = DateTime.UtcNow });
            await db.SaveChangesAsync(ct);
        }

        if (!await db.Users.AnyAsync(u => u.Email == "buh@local", ct))
        {
            var accRole = await db.Roles.SingleAsync(r => r.Name == AppRoles.Accountant, ct);
            var user = new AppUser
            {
                Email = "buh@local",
                FullName = "Бухгалтер",
                PasswordHash = passwords.Hash("ChangeMe!1"),
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            db.Users.Add(user);
            await db.SaveChangesAsync(ct);
            db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = accRole.Id, AssignedAt = DateTime.UtcNow });
            await db.SaveChangesAsync(ct);
        }

        await SeedDefaultCalendarAsync(db, ct);
    }

    private static async Task SeedDefaultCalendarAsync(ApplicationDbContext db, CancellationToken ct)
    {
        if (await db.BusinessCalendarDays.AnyAsync(c => c.CalendarCode == "default", ct))
            return;

        var start = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddYears(-1));
        var end = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddYears(2));
        for (var d = start; d <= end; d = d.AddDays(1))
        {
            var dow = d.DayOfWeek;
            var isWork = dow is not DayOfWeek.Saturday and not DayOfWeek.Sunday;
            db.BusinessCalendarDays.Add(new BusinessCalendarDay
            {
                CalendarCode = "default",
                CalendarDate = d,
                IsWorkingDay = isWork,
                Note = null,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }
        await db.SaveChangesAsync(ct);
    }
}
