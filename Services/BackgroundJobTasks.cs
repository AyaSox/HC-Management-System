using HRManagementSystem.Data;
using HRManagementSystem.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Globalization;

namespace HRManagementSystem.Services
{
    public interface IBackgroundJobTasks
    {
        Task SendBirthdayReminders();
        Task SendAnniversaryReminders();
        Task GenerateMonthlyHeadcountReport();
        Task GenerateSalaryBandReport();
    }

    public class BackgroundJobTasks : IBackgroundJobTasks
    {
        private readonly AppDbContext _db;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<BackgroundJobTasks> _logger;

        public BackgroundJobTasks(AppDbContext db, IWebHostEnvironment env, ILogger<BackgroundJobTasks> logger)
        {
            _db = db;
            _env = env;
            _logger = logger;
        }

        public async Task SendBirthdayReminders()
        {
            var today = DateTime.Today;
            var until = today.AddDays(30);

            var employees = await _db.Employees.AsNoTracking()
                .Where(e => !e.IsDeleted && e.DateOfBirth != null)
                .ToListAsync();

            var upcoming = employees
                .Select(e => new
                {
                    Emp = e,
                    NextBirthday = NextOccurrence(e.DateOfBirth!.Value, today)
                })
                .Where(x => x.NextBirthday <= until)
                .OrderBy(x => x.NextBirthday)
                .ToList();

            if (upcoming.Count == 0)
            {
                _logger.LogInformation("No birthdays in the next 30 days.");
                return;
            }

            foreach (var u in upcoming)
            {
                _logger.LogInformation("?? Upcoming birthday: {Name} on {Date}", u.Emp.FullName, u.NextBirthday.ToString("yyyy-MM-dd"));
            }
        }

        public async Task SendAnniversaryReminders()
        {
            var today = DateTime.Today;
            var until = today.AddDays(30);

            var employees = await _db.Employees.AsNoTracking()
                .Where(e => !e.IsDeleted)
                .ToListAsync();

            var upcoming = employees
                .Select(e => new
                {
                    Emp = e,
                    NextAnniversary = NextOccurrence(e.DateHired, today),
                    Years = YearsBetween(e.DateHired, today)
                })
                .Where(x => x.NextAnniversary <= until)
                .OrderBy(x => x.NextAnniversary)
                .ToList();

            if (upcoming.Count == 0)
            {
                _logger.LogInformation("No anniversaries in the next 30 days.");
                return;
            }

            foreach (var u in upcoming)
            {
                _logger.LogInformation("?? Upcoming anniversary: {Name} ({Years} years) on {Date}", u.Emp.FullName, u.Years, u.NextAnniversary.ToString("yyyy-MM-dd"));
            }
        }

        public async Task GenerateMonthlyHeadcountReport()
        {
            var now = DateTime.Now;
            var path = EnsureReportsFolder();
            var file = Path.Combine(path, $"headcount_{now:yyyyMM}.csv");

            var employees = await _db.Employees.AsNoTracking()
                .Include(e => e.Department)
                .Where(e => !e.IsDeleted)
                .ToListAsync();

            var byDept = employees
                .GroupBy(e => e.Department?.Name ?? "(No Department)")
                .Select(g => new
                {
                    Department = g.Key,
                    Total = g.Count(),
                    Active = g.Count(e => e.Status == EmployeeStatus.Active),
                    OnLeave = g.Count(e => e.Status == EmployeeStatus.OnLeave),
                    Inactive = g.Count(e => e.Status == EmployeeStatus.Inactive)
                })
                .OrderBy(r => r.Department)
                .ToList();

            using var sw = new StreamWriter(file, false);
            await sw.WriteLineAsync("Department,Total,Active,OnLeave,Inactive");
            foreach (var r in byDept)
            {
                await sw.WriteLineAsync($"{Escape(r.Department)},{r.Total},{r.Active},{r.OnLeave},{r.Inactive}");
            }

            _logger.LogInformation("?? Headcount report generated: {File}", file);
        }

        public async Task GenerateSalaryBandReport()
        {
            var now = DateTime.Now;
            var path = EnsureReportsFolder();
            var file = Path.Combine(path, $"salary_bands_{now:yyyyMMdd}.csv");

            var employees = await _db.Employees.AsNoTracking()
                .Where(e => !e.IsDeleted)
                .ToListAsync();

            var bands = new (string Key, decimal Min, decimal? Max)[]
            {
                ("R0–R250k", 0, 250000),
                ("R250k–R400k", 250000, 400000),
                ("R400k–R600k", 400000, 600000),
                ("R600k–R900k", 600000, 900000),
                ("R900k–R1.2m", 900000, 1200000),
                ("R1.2m–R1.8m", 1200000, 1800000),
                ("R1.8m–R3m", 1800000, 3000000),
                ("R3m+", 3000000, null)
            };

            using var sw = new StreamWriter(file, false);
            await sw.WriteLineAsync("Band,Count");
            foreach (var b in bands)
            {
                var count = employees.Count(e => e.Salary >= b.Min && (!b.Max.HasValue || e.Salary <= b.Max.Value));
                await sw.WriteLineAsync($"{b.Key},{count}");
            }

            _logger.LogInformation("?? Salary band report generated: {File}", file);
        }

        private DateTime NextOccurrence(DateTime originalDate, DateTime fromDate)
        {
            var next = new DateTime(fromDate.Year, originalDate.Month, Math.Min(originalDate.Day, DateTime.DaysInMonth(fromDate.Year, originalDate.Month)));
            if (next < fromDate)
            {
                var year = fromDate.Year + 1;
                next = new DateTime(year, originalDate.Month, Math.Min(originalDate.Day, DateTime.DaysInMonth(year, originalDate.Month)));
            }
            return next;
        }

        private int YearsBetween(DateTime start, DateTime now)
        {
            var years = now.Year - start.Year;
            if (new DateTime(now.Year, start.Month, Math.Min(start.Day, DateTime.DaysInMonth(now.Year, start.Month))) > now)
                years--;
            return Math.Max(0, years);
        }

        private string EnsureReportsFolder()
        {
            var folder = Path.Combine(_env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot"), "reports");
            Directory.CreateDirectory(folder);
            return folder;
        }

        private static string Escape(string input)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;
            return input.Contains(',') ? $"\"{input.Replace("\"", "\"\"")}\"" : input;
        }
    }
}
