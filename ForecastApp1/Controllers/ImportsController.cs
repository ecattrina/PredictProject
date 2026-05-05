using ForecastApp1.Data;
using ForecastApp1.Data.Entities;
using ForecastApp1.Dtos;
using ForecastApp1.Services;
using ForecastApp1.Services.Import;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ForecastApp1.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = $"{Security.AppRoles.Admin},{Security.AppRoles.Accountant}")]
public class ImportsController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IExcelImportService _excel;
    private readonly IImportValidationService _validation;
    private readonly IImportCommitService _commit;
    private readonly IImportRollbackService _rollback;
    private readonly IWebHostEnvironment _env;
    private readonly IConfiguration _config;
    private readonly ICurrentUser _user;
    private readonly IAuditService _audit;

    public ImportsController(
        ApplicationDbContext db,
        IExcelImportService excel,
        IImportValidationService validation,
        IImportCommitService commit,
        IImportRollbackService rollback,
        IWebHostEnvironment env,
        IConfiguration config,
        ICurrentUser user,
        IAuditService audit)
    {
        _db = db;
        _excel = excel;
        _validation = validation;
        _commit = commit;
        _rollback = rollback;
        _env = env;
        _config = config;
        _user = user;
        _audit = audit;
    }

    [HttpPost("upload")]
    [RequestSizeLimit(52_428_800)]
    public async Task<ActionResult> Upload(IFormFile file, CancellationToken ct)
    {
        if (file.Length == 0) return BadRequest(new { message = "Пустой файл" });
        var max = _config.GetValue("Forecast:MaxExcelBytes", 52_428_800);
        if (file.Length > max) return BadRequest(new { message = "Файл слишком большой" });
        if (!file.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { message = "Нужен файл .xlsx" });
        var mime = file.ContentType ?? "";
        if (mime.Length > 0 && !mime.Contains("spreadsheet", StringComparison.OrdinalIgnoreCase) && mime != "application/octet-stream")
            return BadRequest(new { message = "Некорректный тип файла" });

        await using var ms = new MemoryStream();
        await file.CopyToAsync(ms, ct);
        ms.Position = 0;
        var hash = ExcelImportService.Sha256(ms);
        ms.Position = 0;

        var dup = await _db.ImportBatches.AnyAsync(b => b.FileHash == hash && b.Status == ImportBatchStatuses.Committed, ct);
        if (dup)
        {
            // предупреждение в аудит, но загрузку разрешаем
            await _audit.WriteAsync("import.duplicate_hash", importBatchId: null, payload: new { hash }, ct: ct);
        }

        var uid = _user.UserId ?? throw new InvalidOperationException("Нет пользователя");
        var relDir = Path.Combine(_config.GetValue("Forecast:ImportUploadsPath", "App_Data/uploads")!, Guid.NewGuid().ToString("N"));
        var relPath = Path.Combine(relDir, SafeFileName(file.FileName));
        Directory.CreateDirectory(Path.Combine(_env.ContentRootPath, relDir));
        var full = Path.Combine(_env.ContentRootPath, relPath);
        await using (var fs = System.IO.File.Create(full))
        {
            ms.Position = 0;
            await ms.CopyToAsync(fs, ct);
        }

        var batch = new ImportBatch
        {
            UploadedByUserId = uid,
            OriginalFileName = file.FileName,
            FileHash = hash,
            FileSizeBytes = file.Length,
            StoragePath = relPath,
            MimeType = file.ContentType,
            Status = ImportBatchStatuses.Uploaded,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.ImportBatches.Add(batch);
        await _db.SaveChangesAsync(ct);

        await _excel.ParseUploadedBatchAsync(batch.Id, ct);
        batch = await _db.ImportBatches.FirstAsync(b => b.Id == batch.Id, ct);
        await _audit.WriteAsync("import.upload", importBatchId: batch.Id, payload: new { file.FileName, hash }, ct: ct);
        return Ok(new { batchId = batch.Id, status = batch.Status });
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ImportBatchDto>>> List(CancellationToken ct)
    {
        var batches = await _db.ImportBatches.AsNoTracking()
            .OrderByDescending(b => b.Id)
            .Take(200)
            .ToListAsync(ct);
        var result = new List<ImportBatchDto>();
        foreach (var b in batches)
        {
            var rc = await _db.ImportRows.CountAsync(r => r.ImportBatchId == b.Id, ct);
            var ec = await _db.ImportErrors.CountAsync(e => e.ImportBatchId == b.Id, ct);
            result.Add(new ImportBatchDto(b.Id, b.OriginalFileName, b.Status, b.FileHash, b.CreatedAt, rc, ec));
        }
        return Ok(result);
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<object>> Get(long id, CancellationToken ct)
    {
        var b = await _db.ImportBatches.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (b is null) return NotFound();
        return Ok(new
        {
            b.Id,
            b.OriginalFileName,
            b.Status,
            b.FileHash,
            b.DetectedBlocksJson,
            b.CommittedAt,
            rows = await _db.ImportRows.CountAsync(r => r.ImportBatchId == id, ct),
            errors = await _db.ImportErrors.CountAsync(e => e.ImportBatchId == id, ct)
        });
    }

    [HttpGet("{id:long}/rows")]
    public async Task<ActionResult> Rows(long id, [FromQuery] int take = 500, CancellationToken ct = default)
    {
        var list = await _db.ImportRows.AsNoTracking()
            .Where(r => r.ImportBatchId == id)
            .OrderBy(r => r.SheetName).ThenBy(r => r.RowNumber)
            .Take(take)
            .Select(r => new { r.Id, r.SheetName, r.RowNumber, r.DetectedEntityType, r.RawJson })
            .ToListAsync(ct);
        return Ok(list);
    }

    [HttpGet("{id:long}/errors")]
    public async Task<ActionResult> Errors(long id, CancellationToken ct)
    {
        var list = await _db.ImportErrors.AsNoTracking()
            .Where(e => e.ImportBatchId == id)
            .OrderBy(e => e.Severity).ThenBy(e => e.RowNumber)
            .Select(e => new { e.Id, e.SheetName, e.RowNumber, e.ErrorCode, e.Severity, e.Message, e.ColumnName })
            .ToListAsync(ct);
        return Ok(list);
    }

    [HttpPost("{id:long}/validate")]
    public async Task<ActionResult<ImportValidationResultDto>> Validate(long id, CancellationToken ct)
    {
        var b = await _db.ImportBatches.FirstAsync(x => x.Id == id, ct);
        if (b.Status == ImportBatchStatuses.Committed)
            return BadRequest(new { message = "Пакет уже подтверждён" });
        var r = await _validation.ValidateAsync(id, ct);
        await _audit.WriteAsync("import.validate", importBatchId: id, payload: r, ct: ct);
        return Ok(r);
    }

    [HttpPost("{id:long}/commit")]
    public async Task<ActionResult> Commit(long id, [FromBody] ImportCommitRequest body, CancellationToken ct)
    {
        var result = await _commit.CommitAsync(id, body.ConfirmWarnings, ct);
        if (!result.Ok) return BadRequest(new { message = result.Message });
        await _audit.WriteAsync("import.commit", importBatchId: id, ct: ct);
        return Ok(new { message = result.Message });
    }

    [HttpPost("{id:long}/rollback")]
    [Authorize(Roles = Security.AppRoles.Admin)]
    public async Task<ActionResult> Rollback(long id, CancellationToken ct)
    {
        var result = await _rollback.RollbackAsync(id, ct);
        if (!result.Ok) return BadRequest(new { message = result.Message });
        await _audit.WriteAsync("import.rollback", importBatchId: id, ct: ct);
        return Ok(new { message = result.Message });
    }

    private static string SafeFileName(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return string.IsNullOrWhiteSpace(name) ? "upload.xlsx" : name;
    }
}
