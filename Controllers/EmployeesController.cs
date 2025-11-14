using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using HRManagementSystem.Data;
using HRManagementSystem.Models;
using HRManagementSystem.Services;
using Microsoft.AspNetCore.Authorization;
using X.PagedList;
using X.PagedList.Extensions;
using ClosedXML.Excel;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Microsoft.Extensions.Caching.Memory;
using HRManagementSystem.Models.ViewModels;
using System.Text.Json;

namespace HRManagementSystem.Controllers
{
    [Authorize]
    public class EmployeesController : Controller
    {
    private readonly AppDbContext _context;
    private readonly AuditService _auditService;
    private readonly IMemoryCache _cache;
    private readonly IUserManagementService _userManagementService;

    private const string FilterSessionKey = "emp_filters";

    public EmployeesController(
        AppDbContext context, 
        AuditService auditService, 
        IMemoryCache cache,
        IUserManagementService userManagementService)
    {
        _context = context;
        _auditService = auditService;
        _cache = cache;
        _userManagementService = userManagementService;
    }

        private class FilterState
        {
            public string? searchString { get; set; }
            public int? departmentId { get; set; }
            public string? sortOrder { get; set; }
            public int? page { get; set; }
            public string? salaryRange { get; set; }
            public decimal? salaryMin { get; set; }
            public decimal? salaryMax { get; set; }
            public EmployeeStatus? status { get; set; }
        }

        // GET: Employees
        public async Task<IActionResult> Index(string? searchString, int? departmentId, string? sortOrder, int? page,
            string? salaryRange, decimal? salaryMin, decimal? salaryMax, EmployeeStatus? status, bool restored = false, bool clear = false)
        {
            // If user requested to clear filters, remove session and redirect to clean Index
            if (clear)
            {
                HttpContext.Session.Remove(FilterSessionKey);
                return RedirectToAction(nameof(Index), new { restored = true });
            }

            bool AllRequestFiltersEmpty() => string.IsNullOrEmpty(searchString)
                && !departmentId.HasValue
                && string.IsNullOrEmpty(sortOrder)
                && !page.HasValue
                && string.IsNullOrEmpty(salaryRange)
                && !salaryMin.HasValue
                && !salaryMax.HasValue
                && !status.HasValue;

            // Restore filters from session if request is empty and we have something saved
            if (!restored && AllRequestFiltersEmpty())
            {
                var saved = HttpContext.Session.GetString(FilterSessionKey);
                if (!string.IsNullOrEmpty(saved))
                {
                    var f = JsonSerializer.Deserialize<FilterState>(saved);
                    bool HasActiveFilters(FilterState s) =>
                        !string.IsNullOrEmpty(s.searchString)
                        || s.departmentId.HasValue
                        || !string.IsNullOrEmpty(s.sortOrder)
                        || s.page.HasValue
                        || !string.IsNullOrEmpty(s.salaryRange)
                        || s.salaryMin.HasValue
                        || s.salaryMax.HasValue
                        || s.status.HasValue;

                    if (f != null && HasActiveFilters(f))
                    {
                        return RedirectToAction(nameof(Index), new
                        {
                            searchString = f.searchString,
                            departmentId = f.departmentId,
                            sortOrder = f.sortOrder,
                            page = f.page,
                            salaryRange = f.salaryRange,
                            salaryMin = f.salaryMin,
                            salaryMax = f.salaryMax,
                            status = f.status,
                            restored = true
                        });
                    }
                }
            }

            // Save current filters (even if empty) to session
            HttpContext.Session.SetString(FilterSessionKey, JsonSerializer.Serialize(new FilterState
            {
                searchString = searchString,
                departmentId = departmentId,
                sortOrder = sortOrder,
                page = page,
                salaryRange = salaryRange,
                salaryMin = salaryMin,
                salaryMax = salaryMax,
                status = status
            }));

            ViewBag.NameSort = string.IsNullOrEmpty(sortOrder) ? "name_desc" : string.Empty;
            ViewBag.DateSort = sortOrder == "date" ? "date_desc" : "date";
            ViewBag.SalarySort = sortOrder == "salary" ? "salary_desc" : "salary";

            var query = _context.Employees
                .AsNoTracking()
                .Where(e => !e.IsDeleted)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchString))
                query = query.Where(e => e.FullName.Contains(searchString) || e.Email.Contains(searchString));
            if (departmentId.HasValue)
                query = query.Where(e => e.DepartmentId == departmentId.Value);
            if (status.HasValue)
                query = query.Where(e => e.Status == status.Value);

            // Apply salary filter
            if (!string.IsNullOrEmpty(salaryRange) && !string.Equals(salaryRange, "custom", StringComparison.OrdinalIgnoreCase))
            {
                var (minBand, maxBand) = ParseSalaryRange(salaryRange);
                if (minBand.HasValue) query = query.Where(e => e.Salary >= minBand.Value);
                if (maxBand.HasValue) query = query.Where(e => e.Salary <= maxBand.Value);
            }
            else
            {
                if (salaryMin.HasValue) query = query.Where(e => e.Salary >= salaryMin.Value);
                if (salaryMax.HasValue) query = query.Where(e => e.Salary <= salaryMax.Value);
            }

            query = sortOrder switch
            {
                "name_desc" => query.OrderByDescending(e => e.FullName),
                "date" => query.OrderBy(e => e.DateHired),
                "date_desc" => query.OrderByDescending(e => e.DateHired),
                "salary" => query.OrderBy(e => (double)e.Salary),
                "salary_desc" => query.OrderByDescending(e => (double)e.Salary),
                _ => query.OrderBy(e => e.FullName)
            };

            var projected = query
                .Select(e => new EmployeeListItem
                {
                    EmployeeId = e.EmployeeId,
                    FullName = e.FullName,
                    JobTitle = e.JobTitle,
                    EmploymentType = e.EmploymentType.ToString(),
                    DepartmentName = e.Department!.Name,
                    Salary = e.Salary,
                    Status = e.Status,
                    DateHired = e.DateHired,
                    ProfilePicturePath = e.ProfilePicturePath,
                    LineManagerId = e.LineManagerId,
                    LineManagerFullName = e.LineManager != null ? e.LineManager.FullName : null
                });

            ViewBag.Departments = await _cache.GetOrCreateAsync("departments_all", async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10);
                return await _context.Departments.AsNoTracking().OrderBy(d => d.Name).ToListAsync();
            });

            ViewBag.CurrentSearch = searchString;
            ViewBag.CurrentDepartment = departmentId;
            ViewBag.CurrentSalaryRange = salaryRange;
            ViewBag.CurrentSalaryMin = salaryMin;
            ViewBag.CurrentSalaryMax = salaryMax;
            ViewBag.CurrentStatus = status;
            ViewBag.SalaryRanges = GetSalaryRanges();

            int pageNumber = page ?? 1;
            const int pageSize = 15;

            var list = await projected.ToListAsync();
            list.ForEach(i => i.FormattedSalary = i.Salary.ToString("C2", System.Globalization.CultureInfo.CurrentCulture));

            return View(list.ToPagedList(pageNumber, pageSize));
        }

        private static (decimal? min, decimal? max) ParseSalaryRange(string range)
        {
            return range switch
            {
                "0-250000" => (0, 250000),
                "250000-400000" => (250000, 400000),
                "400000-600000" => (400000, 600000),
                "600000-900000" => (600000, 900000),
                "900000-1200000" => (900000, 1200000),
                "1200000-1800000" => (1200000, 1800000),
                "1800000-3000000" => (1800000, 3000000),
                "3000000+" => (3000000, null),
                "custom" => (null, null),
                _ => (null, null)
            };
        }

        private static List<SelectListItem> GetSalaryRanges()
        {
            return new List<SelectListItem>
            {
                new() { Value = "", Text = "All Salary Bands" },
                new() { Value = "0-250000", Text = "R0 – R250k" },
                new() { Value = "250000-400000", Text = "R250k – R400k" },
                new() { Value = "400000-600000", Text = "R400k – R600k" },
                new() { Value = "600000-900000", Text = "R600k – R900k" },
                new() { Value = "900000-1200000", Text = "R900k – R1.2m" },
                new() { Value = "1200000-1800000", Text = "R1.2m – R1.8m" },
                new() { Value = "1800000-3000000", Text = "R1.8m – R3m" },
                new() { Value = "3000000+", Text = "R3m+" },
                new() { Value = "custom", Text = "Custom" }
            };
        }

        // GET: Employees/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
                return NotFound();

            var employee = await _context.Employees
                .AsNoTracking()
                .Include(e => e.Department)
                .Include(e => e.LineManager)
                    .ThenInclude(lm => lm.Department)
                .Include(e => e.DirectReports)
                .FirstOrDefaultAsync(m => m.EmployeeId == id);

            if (employee == null)
                return NotFound();

            // Check for pending status change requests
            var hasPendingStatusChange = await _context.StatusChangeRequests
                .AnyAsync(r => r.EmployeeId == id && r.Status == StatusChangeRequestStatus.Pending);
            ViewBag.HasPendingStatusChange = hasPendingStatusChange;

            // Get department statistics
            if (employee.DepartmentId > 0)
            {
                var deptEmployees = await _context.Employees
                    .AsNoTracking()
                    .Where(e => e.DepartmentId == employee.DepartmentId && !e.IsDeleted)
                    .ToListAsync();

                var deptStats = new
                {
                    TotalEmployees = deptEmployees.Count,
                    AverageSalary = deptEmployees.Average(e => e.Salary).ToString("C0"),
                    SalaryRank = deptEmployees.OrderByDescending(e => e.Salary).ToList().FindIndex(e => e.EmployeeId == employee.EmployeeId) + 1,
                    AverageTenure = Math.Round(deptEmployees.Average(e => (DateTime.Now - e.DateHired).TotalDays / 365.25), 1)
                };

                ViewBag.DepartmentStats = deptStats;
            }

            return View(employee);
        }

        // GET: Employees/Create
        public async Task<IActionResult> Create()
        {
            await PopulateDropdownsAsync();
            return View();
        }

        // POST: Employees/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Employee employee)
        {
            ValidateEmployeeBusinessRules(employee, isEdit: false);

            if (ModelState.IsValid)
            {
                await SaveProfilePictureAsync(employee);

                _context.Add(employee);
                await _context.SaveChangesAsync();

                await _auditService.LogEmployeeChangeAsync("INSERT", employee);

                // Create Identity user for ESS Login
                var (success, tempPassword, errorMessage) = await _userManagementService
                    .CreateIdentityUserForEmployeeAsync(employee.Email, employee.FullName);

                if (success)
                {
                    TempData["Success"] = $"Welcome aboard! {employee.FullName} has been successfully hired as {employee.JobTitle ?? "an employee"}. " +
                                         $"ESS Login Created: {employee.Email} | Temporary Password: {tempPassword} " +
                                         $"(Please share these credentials securely with the employee)";
                }
                else
                {
                    TempData["Warning"] = $"{employee.FullName} has been hired, but ESS login creation failed: {errorMessage}. " +
                                         $"Please create the login manually or contact IT support.";
                }

                return RedirectToAction(nameof(Index));
            }

            await PopulateDropdownsAsync(employee.DepartmentId, employee.LineManagerId);
            return View(employee);
        }

        // GET: Employees/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
                return NotFound();

            var employee = await _context.Employees.FindAsync(id);
            if (employee == null)
                return NotFound();

            await PopulateDropdownsAsync(employee.DepartmentId, employee.LineManagerId, excludeEmployeeId: employee.EmployeeId);
            return View(employee);
        }

        // POST: Employees/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Employee employee)
        {
            if (id != employee.EmployeeId)
                return NotFound();

            ValidateEmployeeBusinessRules(employee, isEdit: true);

            if (ModelState.IsValid)
            {
                try
                {
                    var oldEmployee = await _context.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.EmployeeId == id);

                    // Cycle guard: prevent self or indirect cycles
                    if (await WouldIntroduceManagerCycle(employee.EmployeeId, employee.LineManagerId))
                    {
                        ModelState.AddModelError("LineManagerId", "Invalid line manager selection (cycle detected).");
                        await PopulateDropdownsAsync(employee.DepartmentId, employee.LineManagerId, excludeEmployeeId: employee.EmployeeId);
                        return View(employee);
                    }

                    await SaveProfilePictureAsync(employee);

                    _context.Update(employee);
                    await _context.SaveChangesAsync();

                    if (oldEmployee != null)
                        await _auditService.LogEmployeeChangeAsync("UPDATE", employee, oldEmployee);

                    var statusMessage = BuildStatusChangeMessage(oldEmployee, employee);
                    TempData["Success"] = $"{employee.FullName}'s details have been successfully updated.{statusMessage}";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!EmployeeExists(employee.EmployeeId))
                        return NotFound();
                    throw;
                }
                return RedirectToAction(nameof(Index));
            }

            await PopulateDropdownsAsync(employee.DepartmentId, employee.LineManagerId, excludeEmployeeId: employee.EmployeeId);
            return View(employee);
        }

        // GET: Employees/Delete/5
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
                return NotFound();

            var employee = await _context.Employees
                .AsNoTracking()
                .Include(e => e.Department)
                .FirstOrDefaultAsync(m => m.EmployeeId == id);
            if (employee == null)
                return NotFound();

            return View(employee);
        }

        // POST: Employees/Delete/5 (soft delete)
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var employee = await _context.Employees.FindAsync(id);
            if (employee != null)
            {
                await _auditService.LogEmployeeChangeAsync("DELETE", employee);
                employee.IsDeleted = true; // soft delete
                await _context.SaveChangesAsync();

                TempData["Warning"] = $"{employee.FullName} has been moved to the recycle bin.";
            }

            return RedirectToAction(nameof(Index));
        }

        // Admin: view soft-deleted employees
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Trash()
        {
            var deleted = await _context.Employees.IgnoreQueryFilters()
                .AsNoTracking()
                .Where(e => e.IsDeleted)
                .Include(e => e.Department)
                .ToListAsync();
            return View(deleted);
        }

        // Admin: restore soft-deleted employee
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Restore(int id)
        {
            var employee = await _context.Employees.IgnoreQueryFilters().FirstOrDefaultAsync(e => e.EmployeeId == id);
            if (employee == null) return NotFound();
            employee.IsDeleted = false;
            await _context.SaveChangesAsync();
            TempData["Success"] = $"{employee.FullName} has been restored.";
            return RedirectToAction(nameof(Trash));
        }

        // Admin: permanently delete
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Purge(int id)
        {
            var employee = await _context.Employees.IgnoreQueryFilters().FirstOrDefaultAsync(e => e.EmployeeId == id);
            if (employee == null) return NotFound();
            _context.Employees.Remove(employee);
            await _context.SaveChangesAsync();
            TempData["Warning"] = $"{employee.FullName} has been permanently removed.";
            return RedirectToAction(nameof(Trash));
        }

        // Export to Excel
        public async Task<IActionResult> ExportExcel()
        {
            var employees = await _context.Employees
                .AsNoTracking()
                .Include(e => e.Department)
                .Include(e => e.LineManager)
                .Where(e => !e.IsDeleted)
                .ToListAsync();

            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Employees");
            
            // Headers - all fields
            ws.Cell(1, 1).Value = "Employee Number";
            ws.Cell(1, 2).Value = "Full Name";
            ws.Cell(1, 3).Value = "Email";
            ws.Cell(1, 4).Value = "Job Title";
            ws.Cell(1, 5).Value = "Employment Type";
            ws.Cell(1, 6).Value = "Department";
            ws.Cell(1, 7).Value = "Date Hired";
            ws.Cell(1, 8).Value = "Date of Birth";
            ws.Cell(1, 9).Value = "Annual Salary";
            ws.Cell(1, 10).Value = "Gender";
            ws.Cell(1, 11).Value = "Status";
            ws.Cell(1, 12).Value = "Emergency Contact Name";
            ws.Cell(1, 13).Value = "Emergency Contact Phone";
            ws.Cell(1, 14).Value = "Line Manager";

            for (int i = 0; i < employees.Count; i++)
            {
                var e = employees[i];
                int r = i + 2;
                ws.Cell(r, 1).Value = e.EmployeeNumber ?? "";
                ws.Cell(r, 2).Value = e.FullName;
                ws.Cell(r, 3).Value = e.Email;
                ws.Cell(r, 4).Value = e.JobTitle ?? "";
                ws.Cell(r, 5).Value = e.EmploymentType.ToString();
                ws.Cell(r, 6).Value = e.Department?.Name ?? "";
                ws.Cell(r, 7).Value = e.DateHired.ToShortDateString();
                ws.Cell(r, 8).Value = e.DateOfBirth?.ToShortDateString() ?? "";
                ws.Cell(r, 9).Value = e.Salary;
                ws.Cell(r, 10).Value = e.Gender ?? "";
                ws.Cell(r, 11).Value = e.Status.ToString();
                ws.Cell(r, 12).Value = e.EmergencyContactName ?? "";
                ws.Cell(r, 13).Value = e.EmergencyContactPhone ?? "";
                ws.Cell(r, 14).Value = e.LineManager?.FullName ?? "";
            }

            // Auto-fit columns
            ws.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            wb.SaveAs(stream);
            return File(stream.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "Employees.xlsx");
        }

        // Export to PDF
        public async Task<IActionResult> ExportPdf()
        {
            var employees = await _context.Employees
                .AsNoTracking()
                .Include(e => e.Department)
                .Include(e => e.LineManager)
                .Where(e => !e.IsDeleted)
                .ToListAsync();

            var doc = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape()); // Landscape for more columns
                    page.Margin(15);
                    page.Header().Text("Employee Report - Complete Details").Bold().FontSize(14).AlignCenter();

                    page.Content().Table(t =>
                    {
                        t.ColumnsDefinition(c =>
                        {
                            c.ConstantColumn(30); // Employee Number
                            c.RelativeColumn(); // Name
                            c.RelativeColumn(); // Email
                            c.RelativeColumn(); // Job Title
                            c.ConstantColumn(50); // Employment Type
                            c.RelativeColumn(); // Department
                            c.ConstantColumn(60); // Date Hired
                            c.ConstantColumn(60); // Date of Birth
                            c.ConstantColumn(70); // Salary
                            c.ConstantColumn(40); // Gender
                            c.ConstantColumn(40); // Status
                        });

                        void Header(string txt) => t.Cell().Element(v => v.Padding(3).Background(Colors.Grey.Lighten3)).Text(txt).Bold().FontSize(8);
                        Header("Emp#");
                        Header("Name");
                        Header("Email");
                        Header("Job Title");
                        Header("Type");
                        Header("Department");
                        Header("Date Hired");
                        Header("DOB");
                        Header("Salary");
                        Header("Gender");
                        Header("Status");

                        foreach (var e in employees)
                        {
                            void Cell(string txt) => t.Cell().Element(v => v.Padding(3).BorderBottom(1)).Text(txt).FontSize(7);
                            Cell(e.EmployeeNumber ?? "");
                            Cell(e.FullName);
                            Cell(e.Email);
                            Cell(e.JobTitle ?? "");
                            Cell(e.EmploymentType.ToString());
                            Cell(e.Department?.Name ?? "");
                            Cell(e.DateHired.ToShortDateString());
                            Cell(e.DateOfBirth?.ToShortDateString() ?? "");
                            Cell(e.Salary.ToString("C"));
                            Cell(e.Gender ?? "");
                            Cell(e.Status.ToString());
                        }
                    });
                });
            });

            var bytes = doc.GeneratePdf();
            return File(bytes, "application/pdf", "Employees.pdf");
        }

        // POST: Bulk Actions
        [HttpPost]
        public async Task<IActionResult> BulkAction(string action, int[] selectedEmployees)
        {
            if (selectedEmployees == null || selectedEmployees.Length == 0)
            {
                TempData["Error"] = "Please select at least one employee.";
                return RedirectToAction(nameof(Index));
            }

            switch (action)
            {
                case "bulkExport":
                    return await BulkExport(selectedEmployees);
                case "bulkActivate":
                    await BulkUpdateStatus(selectedEmployees, EmployeeStatus.Active);
                    TempData["Success"] = $"Updated {selectedEmployees.Length} employees to Active status.";
                    break;
                case "bulkDeactivate":
                    await BulkUpdateStatus(selectedEmployees, EmployeeStatus.Inactive);
                    TempData["Success"] = $"Updated {selectedEmployees.Length} employees to Inactive status.";
                    break;
                default:
                    TempData["Error"] = "Invalid action selected.";
                    break;
            }

            return RedirectToAction(nameof(Index));
        }

        private async Task BulkUpdateStatus(int[] employeeIds, EmployeeStatus status)
        {
            var employees = await _context.Employees.Where(e => employeeIds.Contains(e.EmployeeId)).ToListAsync();

            foreach (var employee in employees)
            {
                var oldEmployee = new Employee
                {
                    EmployeeId = employee.EmployeeId,
                    Status = employee.Status
                };

                employee.Status = status;
                await _auditService.LogEmployeeChangeAsync("UPDATE", employee, oldEmployee);
            }

            await _context.SaveChangesAsync();
        }

        private async Task<IActionResult> BulkExport(int[] employeeIds)
        {
            var employees = await _context.Employees
                .AsNoTracking()
                .Include(e => e.Department)
                .Include(e => e.LineManager)
                .Where(e => employeeIds.Contains(e.EmployeeId))
                .ToListAsync();

            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Selected Employees");
            ws.Cell(1, 1).Value = "Employee Number";
            ws.Cell(1, 2).Value = "Full Name";
            ws.Cell(1, 3).Value = "Job Title";
            ws.Cell(1, 4).Value = "Employment Type";
            ws.Cell(1, 5).Value = "Email";
            ws.Cell(1, 6).Value = "Department";
            ws.Cell(1, 7).Value = "Line Manager";
            ws.Cell(1, 8).Value = "Annual Salary";
            ws.Cell(1, 9).Value = "Status";
            ws.Cell(1, 10).Value = "Date Hired";

            for (int i = 0; i < employees.Count; i++)
            {
                var e = employees[i];
                int r = i + 2;
                ws.Cell(r, 1).Value = e.EmployeeNumber ?? "Not Assigned";
                ws.Cell(r, 2).Value = e.FullName;
                ws.Cell(r, 3).Value = e.JobTitle ?? "Not Specified";
                ws.Cell(r, 4).Value = e.EmploymentType.ToString();
                ws.Cell(r, 5).Value = e.Email;
                ws.Cell(r, 6).Value = e.Department?.Name;
                ws.Cell(r, 7).Value = e.LineManager?.FullName ?? "None";
                ws.Cell(r, 8).Value = e.Salary;
                ws.Cell(r, 9).Value = e.Status.ToString();
                ws.Cell(r, 10).Value = e.DateHired.ToShortDateString();
            }

            using var stream = new MemoryStream();
            wb.SaveAs(stream);
            return File(stream.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "SelectedEmployees.xlsx");
        }

        // Export to CSV
        public async Task<IActionResult> ExportCsv()
        {
            var employees = await _context.Employees
                .AsNoTracking()
                .Include(e => e.Department)
                .Include(e => e.LineManager)
                .Where(e => !e.IsDeleted)
                .ToListAsync();

            var csv = new System.Text.StringBuilder();
            csv.AppendLine("EmployeeNumber,FullName,Email,JobTitle,EmploymentType,Department,DateHired,DateOfBirth,Salary,Gender,Status,EmergencyContactName,EmergencyContactPhone,LineManager");

            foreach (var e in employees)
            {
                csv.AppendLine($"{EscapeCsv(e.EmployeeNumber)},{EscapeCsv(e.FullName)},{EscapeCsv(e.Email)},{EscapeCsv(e.JobTitle)},{e.EmploymentType},{EscapeCsv(e.Department?.Name)},{e.DateHired:yyyy-MM-dd},{e.DateOfBirth?.ToString("yyyy-MM-dd")},{e.Salary},{EscapeCsv(e.Gender)},{e.Status},{EscapeCsv(e.EmergencyContactName)},{EscapeCsv(e.EmergencyContactPhone)},{EscapeCsv(e.LineManager?.FullName)}");
            }

            return File(System.Text.Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", "Employees.csv");
        }

        // Download template
        [Authorize(Roles = "Admin")]
        public IActionResult DownloadTemplate()
        {
            var csv = new System.Text.StringBuilder();
            csv.AppendLine("EmployeeNumber,FullName,Email,JobTitle,EmploymentType,Department,DateHired,DateOfBirth,Salary,Gender,Status,EmergencyContactName,EmergencyContactPhone,LineManager");
            csv.AppendLine("EMP100,John Doe,john.doe@company.co.za,Developer,Permanent,Information Technology,2024-01-15,1990-05-15,600000,Male,Active,Jane Doe,+27 82 555 0123,");
            csv.AppendLine("EMP101,Jane Smith,jane.smith@company.co.za,Manager,Contract,Finance & Accounting,2023-12-01,1985-03-20,750000,Female,Active,John Smith,+27 83 555 0456,Sarah Johnson");
            csv.AppendLine("EMP102,Mike Johnson,mike.johnson@company.co.za,Sales Representative,Permanent,Sales & Marketing,2024-02-01,1988-12-10,550000,Male,Active,Lisa Johnson,+27 84 555 0789,");

            return File(System.Text.Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", "EmployeeTemplate.csv");
        }

        private static string EscapeCsv(string? field)
        {
            if (string.IsNullOrEmpty(field)) return "";
            if (field.Contains(",") || field.Contains("\"") || field.Contains("\n"))
            {
                return "\"" + field.Replace("\"", "\"\"") + "\"";
            }
            return field;
        }

        private bool EmployeeExists(int id) => _context.Employees.Any(e => e.EmployeeId == id);

        private async Task SaveProfilePictureAsync(Employee employee)
        {
            if (employee.ProfilePicture == null)
                return;

            var folder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images");
            Directory.CreateDirectory(folder);
            var fileName = $"{Guid.NewGuid()}{Path.GetExtension(employee.ProfilePicture.FileName)}";
            var path = Path.Combine(folder, fileName);
            await using var stream = System.IO.File.Create(path);
            await employee.ProfilePicture.CopyToAsync(stream);
            employee.ProfilePicturePath = $"/images/{fileName}";
        }

        private async Task PopulateDropdownsAsync(int? selectedDepartmentId = null, int? selectedLineManagerId = null, int? excludeEmployeeId = null)
        {
            var departments = await _cache.GetOrCreateAsync("departments_all", async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10);
                return await _context.Departments.AsNoTracking().OrderBy(d => d.Name).ToListAsync();
            });
            ViewBag.DepartmentId = new SelectList(departments, "DepartmentId", "Name", selectedDepartmentId);

            var employeesQuery = _context.Employees.AsNoTracking();
            if (excludeEmployeeId.HasValue)
                employeesQuery = employeesQuery.Where(e => e.EmployeeId != excludeEmployeeId.Value);

            var managers = await employeesQuery.OrderBy(e => e.FullName).ToListAsync();
            ViewBag.LineManagerId = new SelectList(managers, "EmployeeId", "FullName", selectedLineManagerId);
        }

        private static string BuildStatusChangeMessage(Employee? oldEmployee, Employee newEmployee)
        {
            if (oldEmployee == null || oldEmployee.Status == newEmployee.Status)
                return string.Empty;

            return newEmployee.Status switch
            {
                EmployeeStatus.Active => " Status updated to Active.",
                EmployeeStatus.Inactive => " Status updated to Inactive.",
                EmployeeStatus.OnLeave => " Status updated to On Leave.",
                _ => string.Empty
            };
        }

        private void ValidateEmployeeBusinessRules(Employee employee, bool isEdit)
        {
            // Date rules
            if (employee.DateOfBirth.HasValue)
            {
                var age = DateTime.Today.Year - employee.DateOfBirth.Value.Year;
                if (employee.DateOfBirth.Value.Date > DateTime.Today.AddYears(-age)) age--;
                if (age < 16)
                    ModelState.AddModelError(nameof(Employee.DateOfBirth), "Employee must be at least 16 years old.");

                if (employee.DateHired < employee.DateOfBirth)
                    ModelState.AddModelError(nameof(Employee.DateHired), "Date Hired must be after Date of Birth.");
            }

            // Salary rule (redundant with DataAnnotations but explicit)
            if (employee.Salary < 0)
                ModelState.AddModelError(nameof(Employee.Salary), "Salary must be a positive number.");

            // Cycle guard for create: only need to ensure not selecting self (if id present)
            if (isEdit && employee.LineManagerId.HasValue && employee.LineManagerId.Value == employee.EmployeeId)
                ModelState.AddModelError(nameof(Employee.LineManagerId), "Employee cannot be their own line manager.");
        }

        private async Task<bool> WouldIntroduceManagerCycle(int employeeId, int? newManagerId)
        {
            if (!newManagerId.HasValue) return false;
            if (newManagerId.Value == employeeId) return true;

            // Walk up the manager chain
            var currentId = newManagerId;
            var visited = new HashSet<int>();
            while (currentId.HasValue)
            {
                if (currentId.Value == employeeId) return true;
                if (!visited.Add(currentId.Value)) break; // safety
                var manager = await _context.Employees.AsNoTracking()
                    .Select(e => new { e.EmployeeId, e.LineManagerId })
                    .FirstOrDefaultAsync(e => e.EmployeeId == currentId.Value);
                currentId = manager?.LineManagerId;
            }
            return false;
        }

        // Import CSV - upload page
        [Authorize(Roles = "Admin")]
        public IActionResult Import()
        {
            return View();
        }

        // Import CSV - preview
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ImportPreview(IFormFile csvFile)
        {
            if (csvFile == null || csvFile.Length == 0)
            {
                TempData["Error"] = "Please upload a CSV file.";
                return RedirectToAction(nameof(Import));
            }

            var rows = new List<EmployeeImportRow>();
            using var reader = new StreamReader(csvFile.OpenReadStream());
            string? header = await reader.ReadLineAsync();
            if (header == null)
            {
                TempData["Error"] = "Empty CSV file.";
                return RedirectToAction(nameof(Import));
            }

            int line = 1;
            while (!reader.EndOfStream)
            {
                var lineText = await reader.ReadLineAsync();
                line++;
                if (string.IsNullOrWhiteSpace(lineText)) continue;
                var parts = SplitCsv(lineText);
                
                // Expected columns: EmployeeNumber,FullName,Email,JobTitle,EmploymentType,Department,DateHired,DateOfBirth,Salary,Gender,Status,EmergencyContactName,EmergencyContactPhone,LineManager
                var row = new EmployeeImportRow
                {
                    EmployeeNumber = parts.ElementAtOrDefault(0) ?? string.Empty,
                    FullName = parts.ElementAtOrDefault(1) ?? string.Empty,
                    Email = parts.ElementAtOrDefault(2) ?? string.Empty,
                    JobTitle = parts.ElementAtOrDefault(3),
                    EmploymentType = parts.ElementAtOrDefault(4),
                    DepartmentName = parts.ElementAtOrDefault(5),
                    DateHiredText = parts.ElementAtOrDefault(6),
                    DateOfBirthText = parts.ElementAtOrDefault(7),
                    SalaryText = parts.ElementAtOrDefault(8),
                    Gender = parts.ElementAtOrDefault(9),
                    Status = parts.ElementAtOrDefault(10),
                    EmergencyContactName = parts.ElementAtOrDefault(11),
                    EmergencyContactPhone = parts.ElementAtOrDefault(12),
                    LineManagerName = parts.ElementAtOrDefault(13)
                };

                // Basic validation
                if (string.IsNullOrWhiteSpace(row.EmployeeNumber)) row.Errors.Add("EmployeeNumber required");
                if (string.IsNullOrWhiteSpace(row.FullName)) row.Errors.Add("FullName required");
                if (string.IsNullOrWhiteSpace(row.Email)) row.Errors.Add("Email required");
                if (string.IsNullOrWhiteSpace(row.DepartmentName)) row.Errors.Add("Department required");
                
                // Date validation
                if (!DateTime.TryParse(row.DateHiredText, out var dateHired)) 
                    row.Errors.Add("Invalid DateHired");
                else 
                    row.DateHired = dateHired;
                
                if (!string.IsNullOrEmpty(row.DateOfBirthText))
                {
                    if (!DateTime.TryParse(row.DateOfBirthText, out var dateOfBirth))
                        row.Errors.Add("Invalid DateOfBirth");
                    else
                        row.DateOfBirth = dateOfBirth;
                }

                // Salary validation
                if (decimal.TryParse(row.SalaryText, out var salary)) 
                    row.Salary = salary; 
                else 
                    row.Errors.Add("Invalid Salary");

                // Employment type validation
                if (!string.IsNullOrEmpty(row.EmploymentType) && !Enum.TryParse<EmploymentType>(row.EmploymentType, true, out _))
                    row.Errors.Add("Invalid EmploymentType");

                // Status validation
                if (!string.IsNullOrEmpty(row.Status) && !Enum.TryParse<EmployeeStatus>(row.Status, true, out _))
                    row.Errors.Add("Invalid Status");

                rows.Add(row);
            }

            var json = JsonSerializer.Serialize(rows);
            TempData["ImportRowsJson"] = json;
            TempData.Keep("ImportRowsJson");

            ViewBag.Rows = rows;
            return View("Import");
        }

        // Import CSV - confirm
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ImportConfirm()
        {
            if (!TempData.ContainsKey("ImportRowsJson"))
            {
                TempData["Error"] = "Import session expired. Please upload the CSV again.";
                return RedirectToAction(nameof(Import));
            }

            var rows = JsonSerializer.Deserialize<List<EmployeeImportRow>>((string)TempData["ImportRowsJson"]!);
            if (rows == null)
            {
                TempData["Error"] = "Import data missing.";
                return RedirectToAction(nameof(Import));
            }

            int created = 0;
            foreach (var r in rows.Where(r => !r.Errors.Any()))
            {
                var dept = await _context.Departments.AsNoTracking().FirstOrDefaultAsync(d => d.Name == r.DepartmentName);
                if (dept == null) continue;

                // Find line manager if specified
                Employee? lineManager = null;
                if (!string.IsNullOrEmpty(r.LineManagerName))
                {
                    lineManager = await _context.Employees.AsNoTracking()
                        .FirstOrDefaultAsync(e => e.FullName == r.LineManagerName);
                }

                var emp = new Employee
                {
                    EmployeeNumber = r.EmployeeNumber,
                    FullName = r.FullName,
                    Email = r.Email,
                    JobTitle = r.JobTitle,
                    EmploymentType = Enum.TryParse<EmploymentType>(r.EmploymentType ?? string.Empty, true, out var et) ? et : EmploymentType.Permanent,
                    DepartmentId = dept.DepartmentId,
                    DateHired = r.DateHired.GetValueOrDefault(DateTime.Today),
                    DateOfBirth = r.DateOfBirth,
                    Salary = r.Salary,
                    Gender = r.Gender,
                    Status = Enum.TryParse<EmployeeStatus>(r.Status ?? string.Empty, true, out var st) ? st : EmployeeStatus.Active,
                    EmergencyContactName = r.EmergencyContactName,
                    EmergencyContactPhone = r.EmergencyContactPhone,
                    LineManagerId = lineManager?.EmployeeId
                };
                
                _context.Employees.Add(emp);
                created++;
            }
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Successfully imported {created} employees with complete information.";
            return RedirectToAction(nameof(Index));
        }

        private static List<string> SplitCsv(string input)
        {
            var list = new List<string>();
            var cur = new System.Text.StringBuilder();
            bool inQuotes = false;
            for (int i = 0; i < input.Length; i++)
            {
                var ch = input[i];
                if (ch == '"')
                {
                    if (inQuotes && i + 1 < input.Length && input[i + 1] == '"') { cur.Append('"'); i++; }
                    else inQuotes = !inQuotes;
                }
                else if (ch == ',' && !inQuotes)
                {
                    list.Add(cur.ToString()); cur.Clear();
                }
                else cur.Append(ch);
            }
            list.Add(cur.ToString());
            return list;
        }
    }
}