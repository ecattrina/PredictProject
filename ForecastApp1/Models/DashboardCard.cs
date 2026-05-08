namespace ForecastApp1.Models;

public sealed class DashboardCard
{
    public required string Title { get; init; }
    public required string Description { get; init; }
    public required string Controller { get; init; }
    public required string Action { get; init; }
}
