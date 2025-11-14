using System.Diagnostics;
using HRManagementSystem.Models;
using Microsoft.AspNetCore.Mvc;
using HRManagementSystem.Data;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace HRManagementSystem.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly AppDbContext _context;

        public HomeController(ILogger<HomeController> logger, AppDbContext context)
        {
            _logger = logger;
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            if (User?.Identity?.IsAuthenticated == true)
            {
                var totalEmployees = await _context.Employees.AsNoTracking().CountAsync();
                var activeEmployees = await _context.Employees.AsNoTracking().CountAsync(e => e.Status == EmployeeStatus.Active);
                var today = DateTime.Today;
                var onLeaveEmployees = await _context.LeaveApplications
                    .AsNoTracking()
                    .Where(l => l.Status == LeaveStatus.Approved && l.StartDate <= today && l.EndDate >= today)
                    .Select(l => l.EmployeeId)
                    .Distinct()
                    .CountAsync();
                var avgSalary = await _context.Employees.AsNoTracking().AverageAsync(e => e.Salary);

                ViewBag.TotalEmployees = totalEmployees;
                ViewBag.ActiveEmployees = activeEmployees;
                ViewBag.OnLeaveEmployees = onLeaveEmployees;
                ViewBag.AvgSalary = avgSalary.ToString("C0", CultureInfo.CurrentCulture);
            }

            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
