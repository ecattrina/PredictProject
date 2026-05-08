using ForecastApp1.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ForecastApp1.Controllers;

[Authorize]
public class AdminController : Controller
{
    [HttpGet]
    public IActionResult Index()
    {
        var cards = new List<DashboardCard>
        {
            new() { Title = "Справочники", Description = "Поставщики и договоры (сводно)", Controller = "Dictionaries", Action = "Index" },
            new() { Title = "Поставщики", Description = "Справочник поставщиков", Controller = "Suppliers", Action = "Index" },
            new() { Title = "Договоры", Description = "Управление договорами", Controller = "Contracts", Action = "Index" },
            new() { Title = "Условия оплаты", Description = "Условия по договорам", Controller = "Conditions", Action = "Index" }
        };

        return View(cards);
    }
}
