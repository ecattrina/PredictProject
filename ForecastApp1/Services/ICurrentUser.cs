namespace ForecastApp1.Services;

public interface ICurrentUser
{
    long? UserId { get; }
    string? Email { get; }
    bool IsInRole(string role);
}

public sealed class HttpCurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _http;

    public HttpCurrentUser(IHttpContextAccessor http) => _http = http;

    public long? UserId
    {
        get
        {
            var v = _http.HttpContext?.User.FindFirst("uid")?.Value;
            return long.TryParse(v, out var id) ? id : null;
        }
    }

    public string? Email => _http.HttpContext?.User.Identity?.Name;

    public bool IsInRole(string role) => _http.HttpContext?.User.IsInRole(role) ?? false;
}
