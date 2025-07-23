using Microsoft.AspNetCore.Mvc;

namespace ForecastApp1.Controllers
{
    [ApiController]
    [Route("api/import")]
    public class ImportController : ControllerBase
    {
        private readonly ExcelImportService _importService;

        public ImportController(ExcelImportService importService)
        {
            _importService = importService;
        }
        [HttpPost("excel")]
        public async Task<IActionResult> UploadExcel(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest("Файл не загружен");

            try
            {
                using var stream = file.OpenReadStream();
                await _importService.ImportAsync(stream);
                return Ok("Импорт завершён");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Ошибка на сервере: {ex.Message}");
            }
        }
    }
}
