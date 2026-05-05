namespace ForecastApp1.Services.Import;

public interface IImportFileStorage
{
    Task SaveToPathAsync(string relativePath, Stream content, CancellationToken ct);
}

public sealed class LocalImportFileStorage : IImportFileStorage
{
    private readonly IWebHostEnvironment _env;

    public LocalImportFileStorage(IWebHostEnvironment env) => _env = env;

    public async Task SaveToPathAsync(string relativePath, Stream content, CancellationToken ct)
    {
        var full = Path.Combine(_env.ContentRootPath, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        await using var fs = File.Create(full);
        await content.CopyToAsync(fs, ct);
    }
}
