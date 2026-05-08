using ForecastApp1.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ForecastApp1.Controllers;

[Authorize(Roles = AppRoles.Admin)]
public class UserManagementController : Controller
{
    [HttpGet]
    public IActionResult Index() => View();
}
