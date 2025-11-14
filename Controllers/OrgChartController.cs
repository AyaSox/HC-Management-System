using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HRManagementSystem.Data;
using Microsoft.AspNetCore.Authorization;

namespace HRManagementSystem.Controllers
{
    [Authorize]
    public class OrgChartController : Controller
    {
        private readonly AppDbContext _context;

        public OrgChartController(AppDbContext context)
        {
            _context = context;
        }

        // GET: OrgChart
        public async Task<IActionResult> Index()
        {
            var employees = await _context.Employees
                .AsNoTracking()
                .Where(e => !e.IsDeleted)
                .Include(e => e.Department)
                .Include(e => e.DirectReports)
                .OrderBy(e => e.FullName)
                .ToListAsync();

            // Build hierarchical structure
            var orgData = employees.Select(e => new
            {
                id = e.EmployeeId,
                name = e.FullName,
                title = e.JobTitle ?? "No Title",
                department = e.Department?.Name ?? "No Department",
                manager = e.LineManagerId,
                employeeNumber = e.EmployeeNumber ?? "",
                email = e.Email,
                status = e.Status.ToString(),
                profilePicture = !string.IsNullOrEmpty(e.ProfilePicturePath) ? e.ProfilePicturePath : null,
                directReports = e.DirectReports?.Count ?? 0
            }).ToList();

            ViewBag.OrgData = System.Text.Json.JsonSerializer.Serialize(orgData);
            ViewBag.TotalEmployees = employees.Count;
            ViewBag.DepartmentCount = employees.Select(e => e.DepartmentId).Distinct().Count();

            return View();
        }

        // GET: OrgChart/Department/5
        public async Task<IActionResult> Department(int id)
        {
            var department = await _context.Departments
                .Include(d => d.Employees.Where(e => !e.IsDeleted))
                .ThenInclude(e => e.DirectReports)
                .FirstOrDefaultAsync(d => d.DepartmentId == id);

            if (department == null)
                return NotFound();

            var employees = department.Employees.ToList();
            
            var orgData = employees.Select(e => new
            {
                id = e.EmployeeId,
                name = e.FullName,
                title = e.JobTitle ?? "No Title",
                department = department.Name,
                manager = e.LineManagerId,
                employeeNumber = e.EmployeeNumber ?? "",
                email = e.Email,
                status = e.Status.ToString(),
                profilePicture = !string.IsNullOrEmpty(e.ProfilePicturePath) ? e.ProfilePicturePath : null,
                directReports = e.DirectReports?.Count ?? 0
            }).ToList();

            ViewBag.OrgData = System.Text.Json.JsonSerializer.Serialize(orgData);
            ViewBag.DepartmentName = department.Name;
            ViewBag.TotalEmployees = employees.Count;

            return View("Index");
        }

        // API endpoint for org chart data
        [HttpGet]
        public async Task<IActionResult> GetOrgData()
        {
            var employees = await _context.Employees
                .AsNoTracking()
                .Where(e => !e.IsDeleted)
                .Include(e => e.Department)
                .Include(e => e.DirectReports)
                .ToListAsync();

            var orgData = employees.Select(e => new
            {
                id = e.EmployeeId,
                name = e.FullName,
                title = e.JobTitle ?? "No Title",
                department = e.Department?.Name ?? "No Department",
                manager = e.LineManagerId,
                employeeNumber = e.EmployeeNumber ?? "",
                email = e.Email,
                status = e.Status.ToString(),
                profilePicture = !string.IsNullOrEmpty(e.ProfilePicturePath) ? e.ProfilePicturePath : null,
                directReports = e.DirectReports?.Count ?? 0
            }).ToList();

            return Json(orgData);
        }
    }
}