using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HRManagementSystem.Data;
using Microsoft.AspNetCore.Authorization;
using System.Globalization;
using HRManagementSystem.Models;

namespace HRManagementSystem.Controllers
{
    [Authorize]
    public class DashboardController : Controller
    {
        private readonly AppDbContext _context;

        public DashboardController(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var today = DateTime.Today;

            // Summary statistics - exclude soft-deleted employees
            var totalEmployees = await _context.Employees.AsNoTracking().Where(e => !e.IsDeleted).CountAsync();
            var totalDepartments = await _context.Departments.AsNoTracking().CountAsync();
            var avgSalaryDecimal = await _context.Employees.AsNoTracking().Where(e => !e.IsDeleted).AverageAsync(e => e.Salary);

            ViewBag.TotalEmployees = totalEmployees;
            ViewBag.TotalDepartments = totalDepartments;
            ViewBag.AvgSalary = avgSalaryDecimal.ToString("C2", CultureInfo.CurrentCulture);

            // Employees per Department - exclude soft-deleted
            var deptData = await _context.Departments
                .AsNoTracking()
                .Select(d => new { d.Name, Count = d.Employees.Count(e => !e.IsDeleted) })
                .ToListAsync();

            ViewBag.DeptLabels = deptData.Select(d => d.Name).ToArray();
            ViewBag.DeptCounts = deptData.Select(d => d.Count).ToArray();

            // Gender distribution - exclude soft-deleted
            var genderData = await _context.Employees
                .AsNoTracking()
                .Where(e => !string.IsNullOrEmpty(e.Gender) && !e.IsDeleted)
                .GroupBy(e => e.Gender)
                .Select(g => new { Gender = g.Key, Count = g.Count() })
                .ToListAsync();

            ViewBag.Genders = genderData.Select(g => g.Gender).ToArray();
            ViewBag.GenderCounts = genderData.Select(g => g.Count).ToArray();

            // Recent hires (last 30 days) - excluding soft-deleted
            var recentHiresQuery = _context.Employees
                .AsNoTracking()
                .Where(e => e.DateHired >= DateTime.Now.AddDays(-30) && !e.IsDeleted)
                .Include(e => e.Department);

            ViewBag.RecentHiresCount = await recentHiresQuery.CountAsync();
            ViewBag.RecentHires = await recentHiresQuery
                .OrderByDescending(e => e.DateHired)
                .Take(5)
                .ToListAsync();

            // Upcoming birthdays (next 30 days) – compute client-side compatible - exclude soft-deleted
            var upcomingBirthdays = await _context.Employees
                .AsNoTracking()
                .Where(e => e.DateOfBirth.HasValue && !e.IsDeleted)
                .Include(e => e.Department)
                .ToListAsync();

            var birthdayList = upcomingBirthdays
                .Where(e => e.HasUpcomingBirthday)
                .OrderBy(e =>
                {
                    var nextBirthday = new DateTime(today.Year, e.DateOfBirth!.Value.Month, e.DateOfBirth.Value.Day);
                    if (nextBirthday < today) nextBirthday = nextBirthday.AddYears(1);
                    return nextBirthday;
                })
                .Take(5)
                .ToList();

            ViewBag.UpcomingBirthdays = birthdayList;

            // Upcoming anniversaries (next 30 days) - exclude soft-deleted
            var employeeAnniversaries = await _context.Employees
                .AsNoTracking()
                .Where(e => !e.IsDeleted)
                .Include(e => e.Department)
                .ToListAsync();

            var anniversaryList = employeeAnniversaries
                .Where(e =>
                {
                    var thisYearAnniversary = new DateTime(today.Year, e.DateHired.Month, e.DateHired.Day);
                    if (thisYearAnniversary < today) thisYearAnniversary = thisYearAnniversary.AddYears(1);
                    var days = (thisYearAnniversary - today).Days;
                    return days <= 30 && days >= 0;
                })
                .OrderBy(e =>
                {
                    var thisYearAnniversary = new DateTime(today.Year, e.DateHired.Month, e.DateHired.Day);
                    if (thisYearAnniversary < today) thisYearAnniversary = thisYearAnniversary.AddYears(1);
                    return thisYearAnniversary;
                })
                .Take(5)
                .ToList();

            ViewBag.UpcomingAnniversaries = anniversaryList;

            // Employee status breakdown - exclude soft-deleted
            var statusData = await _context.Employees
                .AsNoTracking()
                .Where(e => !e.IsDeleted)
                .GroupBy(e => e.Status)
                .Select(g => new { Status = g.Key.ToString(), Count = g.Count() })
                .ToListAsync();

            ViewBag.StatusLabels = statusData.Select(s => s.Status).ToArray();
            ViewBag.StatusCounts = statusData.Select(s => s.Count).ToArray();

            // Upcoming leave (next 30 days): Show employees with Status = OnLeave or approved leave applications
            var upcomingLeave = await _context.LeaveApplications
                .AsNoTracking()
                .Where(la => la.Status == LeaveStatus.Approved
                             && la.EndDate >= today
                             && la.StartDate <= today.AddDays(30))
                .Include(la => la.Employee)
                    .ThenInclude(e => e.Department)
                .Include(la => la.LeaveType)
                .OrderBy(la => la.StartDate)
                .Take(8)
                .ToListAsync();

            ViewBag.UpcomingLeave = upcomingLeave;

            return View();
        }
    }
}