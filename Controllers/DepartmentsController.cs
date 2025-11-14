using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HRManagementSystem.Data;
using HRManagementSystem.Models;
using Microsoft.AspNetCore.Authorization;
using HRManagementSystem.Services;

namespace HRManagementSystem.Controllers
{
    [Authorize]
    public class DepartmentsController : Controller
    {
        private readonly AppDbContext _context;
        private readonly AuditService _auditService;

        public DepartmentsController(AppDbContext context, AuditService auditService)
        {
            _context = context;
            _auditService = auditService;
        }

        // GET: Departments
        public async Task<IActionResult> Index()
        {
            var departments = await _context.Departments
                .AsNoTracking()
                .OrderBy(d => d.Name)
                .ToListAsync();
            return View(departments);
        }

        // GET: Departments/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
                return NotFound();

            var department = await _context.Departments
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.DepartmentId == id);

            if (department == null)
                return NotFound();

            return View(department);
        }

        // GET: Departments/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Departments/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Department department)
        {
            if (!ModelState.IsValid)
                return View(department);

            _context.Add(department);
            await _context.SaveChangesAsync();

            await _auditService.LogDepartmentChangeAsync("INSERT", department);

            TempData["Success"] = $"Department '{department.Name}' created successfully.";
            return RedirectToAction(nameof(Index));
        }

        // GET: Departments/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
                return NotFound();

            var department = await _context.Departments.FindAsync(id);
            if (department == null)
                return NotFound();

            return View(department);
        }

        // POST: Departments/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Department department)
        {
            if (id != department.DepartmentId)
                return NotFound();

            if (!ModelState.IsValid)
                return View(department);

            try
            {
                var oldDepartment = await _context.Departments
                    .AsNoTracking()
                    .FirstOrDefaultAsync(d => d.DepartmentId == id);

                _context.Update(department);
                await _context.SaveChangesAsync();

                if (oldDepartment != null)
                    await _auditService.LogDepartmentChangeAsync("UPDATE", department, oldDepartment);

                TempData["Success"] = $"Department '{department.Name}' updated successfully.";
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!DepartmentExists(department.DepartmentId))
                    return NotFound();
                throw;
            }

            return RedirectToAction(nameof(Index));
        }

        // GET: Departments/Delete/5
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
                return NotFound();

            var department = await _context.Departments
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.DepartmentId == id);

            if (department == null)
                return NotFound();

            return View(department);
        }

        // POST: Departments/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var department = await _context.Departments.FindAsync(id);
            if (department != null)
            {
                await _auditService.LogDepartmentChangeAsync("DELETE", department);

                _context.Departments.Remove(department);
                await _context.SaveChangesAsync();
                TempData["Warning"] = $"Department '{department.Name}' has been removed.";
            }

            return RedirectToAction(nameof(Index));
        }

        // GET: Department Analytics
        public async Task<IActionResult> Analytics()
        {
            var departments = await _context.Departments
                .Include(d => d.Employees.Where(e => !e.IsDeleted))
                .ToListAsync();

            var analytics = departments.Select(d => new
            {
                DepartmentName = d.Name,
                TotalEmployees = d.Employees.Count,
                ActiveEmployees = d.Employees.Count(e => e.Status == EmployeeStatus.Active),
                OnLeaveEmployees = d.Employees.Count(e => e.Status == EmployeeStatus.OnLeave),
                InactiveEmployees = d.Employees.Count(e => e.Status == EmployeeStatus.Inactive),
                AverageSalary = d.Employees.Any() ? d.Employees.Average(e => e.Salary) : 0,
                MedianSalary = d.Employees.Any() ? GetMedianSalary(d.Employees.Select(e => e.Salary).ToList()) : 0,
                HighestSalary = d.Employees.Any() ? d.Employees.Max(e => e.Salary) : 0,
                LowestSalary = d.Employees.Any() ? d.Employees.Min(e => e.Salary) : 0,
                AverageTenure = d.Employees.Any() ? Math.Round(d.Employees.Average(e => (DateTime.Now - e.DateHired).TotalDays / 365.25), 1) : 0,
                RecentHires = d.Employees.Count(e => e.DateHired >= DateTime.Now.AddDays(-30)),
                PermanentEmployees = d.Employees.Count(e => e.EmploymentType == EmploymentType.Permanent),
                ContractEmployees = d.Employees.Count(e => e.EmploymentType == EmploymentType.Contract),
                TemporaryEmployees = d.Employees.Count(e => e.EmploymentType == EmploymentType.Temporary)
            }).OrderByDescending(a => a.TotalEmployees).ToList();

            // Overall company stats
            var allEmployees = await _context.Employees.Where(e => !e.IsDeleted).ToListAsync();
            ViewBag.CompanyStats = new
            {
                TotalEmployees = allEmployees.Count,
                TotalDepartments = departments.Count,
                CompanyAverageSalary = allEmployees.Any() ? allEmployees.Average(e => e.Salary) : 0,
                CompanyAverageTenure = allEmployees.Any() ? Math.Round(allEmployees.Average(e => (DateTime.Now - e.DateHired).TotalDays / 365.25), 1) : 0
            };

            return View(analytics);
        }

        private static decimal GetMedianSalary(List<decimal> salaries)
        {
            if (!salaries.Any()) return 0;
            
            var sorted = salaries.OrderBy(s => s).ToList();
            int count = sorted.Count;
            
            if (count % 2 == 0)
                return (sorted[count / 2 - 1] + sorted[count / 2]) / 2;
            else
                return sorted[count / 2];
        }

        private bool DepartmentExists(int id) => _context.Departments.Any(e => e.DepartmentId == id);

        // GET: Departments/OrgChart/5
        public async Task<IActionResult> OrgChart(int id)
        {
            return RedirectToAction("Department", "OrgChart", new { id = id });
        }
    }
}