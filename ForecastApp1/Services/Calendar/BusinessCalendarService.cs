using ForecastApp1.Data;
using Microsoft.EntityFrameworkCore;

namespace ForecastApp1.Services.Calendar;

public interface IBusinessCalendarService
{
    Task<DateOnly> NextWorkingDayOnOrAfterAsync(DateOnly date, string calendarCode, CancellationToken ct);
}

public sealed class BusinessCalendarService : IBusinessCalendarService
{
    private readonly ApplicationDbContext _db;

    public BusinessCalendarService(ApplicationDbContext db) => _db = db;

    public async Task<DateOnly> NextWorkingDayOnOrAfterAsync(DateOnly date, string calendarCode, CancellationToken ct)
    {
        for (var i = 0; i < 1200; i++)
        {
            var row = await _db.BusinessCalendarDays.AsNoTracking()
                .FirstOrDefaultAsync(c => c.CalendarCode == calendarCode && c.CalendarDate == date && c.DeletedAt == null, ct);
            var working = row?.IsWorkingDay ?? IsDefaultWorking(date);
            if (working) return date;
            date = date.AddDays(1);
        }
        return date;
    }

    private static bool IsDefaultWorking(DateOnly d) =>
        d.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday;
}
