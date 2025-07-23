using ForecastApp1.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ForecastApp1.Controllers
{
    [ApiController]
    [Route("api/payments")]
    public class PaymentsController : ControllerBase
    {
        private readonly PaymentScheduleService _service;

        public PaymentsController(PaymentScheduleService service)
        {
            _service = service;
        }

        [HttpGet("schedule")]
        public async Task<IActionResult> GetPaymentSchedule([FromQuery] DateTime startDate)
        {
            var result = await _service.GetPaymentScheduleAsync(startDate);
            return Ok(result);
        }
    }
}
