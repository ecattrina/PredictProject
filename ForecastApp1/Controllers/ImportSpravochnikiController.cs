using ForecastApp1.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ForecastApp1.Controllers;

[Authorize(Roles = $"{AppRoles.Admin},{AppRoles.Accountant}")]
public class ImportSpravochnikiController : Controller
{
    [HttpGet]
    public IActionResult Index() => View();
}
