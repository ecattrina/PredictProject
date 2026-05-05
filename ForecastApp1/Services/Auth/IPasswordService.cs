using Microsoft.AspNetCore.Identity;

namespace ForecastApp1.Services.Auth;

public interface IPasswordService
{
    string Hash(string password);
    bool Verify(string password, string hash);
}

public sealed class PasswordService : IPasswordService
{
    private readonly PasswordHasher<object> _hasher = new();

    public string Hash(string password) => _hasher.HashPassword(new object(), password);

    public bool Verify(string password, string hash)
    {
        var r = _hasher.VerifyHashedPassword(new object(), hash, password);
        return r == PasswordVerificationResult.Success || r == PasswordVerificationResult.SuccessRehashNeeded;
    }
}
