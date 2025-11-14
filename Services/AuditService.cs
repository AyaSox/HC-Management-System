using HRManagementSystem.Models;
using HRManagementSystem.Data;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using System.Text.Json;

namespace HRManagementSystem.Services
{
    public class AuditService : IAuditService
    {
        private readonly AppDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public AuditService(AppDbContext context, IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
        }

        // Simplified logging method for Leave Management
        public async Task LogAsync(string description, string entityType, string entityId)
        {
            var user = _httpContextAccessor.HttpContext?.User;
            var userId = user?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "System";
            var userName = user?.Identity?.Name ?? "System";

            var auditLog = new AuditLog
            {
                TableName = entityType,
                Action = description,
                RecordId = int.TryParse(entityId, out var id) ? id : null,
                UserId = userId,
                UserName = userName,
                Changes = description,
                Timestamp = DateTime.UtcNow
            };

            _context.AuditLogs.Add(auditLog);
            await _context.SaveChangesAsync();
        }

        public async Task LogAsync(string tableName, string action, int? recordId, object? oldValues = null, object? newValues = null, string? changes = null)
        {
            var user = _httpContextAccessor.HttpContext?.User;
            var userId = user?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "System";
            var userName = user?.Identity?.Name ?? "System";

            var auditLog = new AuditLog
            {
                TableName = tableName,
                Action = action,
                RecordId = recordId,
                OldValues = oldValues != null ? JsonSerializer.Serialize(oldValues) : null,
                NewValues = newValues != null ? JsonSerializer.Serialize(newValues) : null,
                UserId = userId,
                UserName = userName,
                Changes = changes,
                Timestamp = DateTime.UtcNow
            };

            _context.AuditLogs.Add(auditLog);
            await _context.SaveChangesAsync();
        }

        public async Task LogEmployeeChangeAsync(string action, Employee employee, Employee? oldEmployee = null)
        {
            const string tableName = "Employees";
            string? changes = null;

            if (oldEmployee != null && action == "UPDATE")
            {
                var changesList = new List<string>();

                if (!string.Equals(oldEmployee.EmployeeNumber, employee.EmployeeNumber, StringComparison.Ordinal))
                    changesList.Add($"Employee Number: '{oldEmployee.EmployeeNumber}' to '{employee.EmployeeNumber}'");
                if (!string.Equals(oldEmployee.FullName, employee.FullName, StringComparison.Ordinal))
                    changesList.Add($"Full Name: '{oldEmployee.FullName}' to '{employee.FullName}'");
                if (!string.Equals(oldEmployee.Email, employee.Email, StringComparison.OrdinalIgnoreCase))
                    changesList.Add($"Email: '{oldEmployee.Email}' to '{employee.Email}'");
                if (!string.Equals(oldEmployee.JobTitle, employee.JobTitle, StringComparison.Ordinal))
                    changesList.Add($"Job Title: '{oldEmployee.JobTitle ?? "None"}' to '{employee.JobTitle ?? "None"}'");
                if (oldEmployee.EmploymentType != employee.EmploymentType)
                    changesList.Add($"Employment Type: '{oldEmployee.EmploymentType}' to '{employee.EmploymentType}'");
                if (oldEmployee.Salary != employee.Salary)
                    changesList.Add($"Salary: {oldEmployee.Salary:C} to {employee.Salary:C}");

                if (oldEmployee.DepartmentId != employee.DepartmentId)
                {
                    var oldDept = await _context.Departments.FindAsync(oldEmployee.DepartmentId);
                    var newDept = await _context.Departments.FindAsync(employee.DepartmentId);
                    changesList.Add($"Department: '{oldDept?.Name ?? "Unknown"}' to '{newDept?.Name ?? "Unknown"}'");
                }

                if (oldEmployee.LineManagerId != employee.LineManagerId)
                {
                    var oldManager = oldEmployee.LineManagerId.HasValue
                        ? await _context.Employees.FindAsync(oldEmployee.LineManagerId.Value)
                        : null;
                    var newManager = employee.LineManagerId.HasValue
                        ? await _context.Employees.FindAsync(employee.LineManagerId.Value)
                        : null;
                    changesList.Add($"Line Manager: '{oldManager?.FullName ?? "None"}' to '{newManager?.FullName ?? "None"}'");
                }

                if (oldEmployee.Status != employee.Status)
                    changesList.Add($"Status: '{oldEmployee.Status}' to '{employee.Status}'");
                if (oldEmployee.DateHired != employee.DateHired)
                    changesList.Add($"Date Hired: {oldEmployee.DateHired:yyyy-MM-dd} to {employee.DateHired:yyyy-MM-dd}");
                if (oldEmployee.DateOfBirth != employee.DateOfBirth)
                    changesList.Add($"Date of Birth: {oldEmployee.DateOfBirth?.ToString("yyyy-MM-dd") ?? "None"} to {employee.DateOfBirth?.ToString("yyyy-MM-dd") ?? "None"}");
                if (!string.Equals(oldEmployee.Gender, employee.Gender, StringComparison.Ordinal))
                    changesList.Add($"Gender: '{oldEmployee.Gender ?? "None"}' to '{employee.Gender ?? "None"}'");
                if (!string.Equals(oldEmployee.EmergencyContactName, employee.EmergencyContactName, StringComparison.Ordinal))
                    changesList.Add($"Emergency Contact Name: '{oldEmployee.EmergencyContactName ?? "None"}' to '{employee.EmergencyContactName ?? "None"}'");
                if (!string.Equals(oldEmployee.EmergencyContactPhone, employee.EmergencyContactPhone, StringComparison.Ordinal))
                    changesList.Add($"Emergency Contact Phone: '{oldEmployee.EmergencyContactPhone ?? "None"}' to '{employee.EmergencyContactPhone ?? "None"}'");

                changes = changesList.Count > 0 ? string.Join("; ", changesList) : "No changes detected";
            }

            var oldValues = oldEmployee != null ? CreateSimplifiedEmployee(oldEmployee) : null;
            var newValues = CreateSimplifiedEmployee(employee);

            await LogAsync(tableName, action, employee.EmployeeId, oldValues, newValues, changes);
        }

        public async Task LogDepartmentChangeAsync(string action, Department department, Department? oldDepartment = null)
        {
            const string tableName = "Departments";
            string? changes = null;

            if (oldDepartment != null && action == "UPDATE")
            {
                var changesList = new List<string>();
                if (!string.Equals(oldDepartment.Name, department.Name, StringComparison.Ordinal))
                    changesList.Add($"Name: '{oldDepartment.Name}' to '{department.Name}'");
                changes = changesList.Count > 0 ? string.Join("; ", changesList) : "No changes detected";
            }

            var oldValues = oldDepartment != null ? new { oldDepartment.DepartmentId, oldDepartment.Name } : null;
            var newValues = new { department.DepartmentId, department.Name };
            await LogAsync(tableName, action, department.DepartmentId, oldValues, newValues, changes);
        }

        private static object CreateSimplifiedEmployee(Employee employee)
        {
            return new
            {
                employee.EmployeeId,
                employee.EmployeeNumber,
                employee.FullName,
                employee.Email,
                employee.JobTitle,
                EmploymentType = employee.EmploymentType.ToString(),
                employee.DateHired,
                employee.DateOfBirth,
                employee.Salary,
                employee.Gender,
                Status = employee.Status.ToString(),
                employee.DepartmentId,
                employee.LineManagerId,
                employee.EmergencyContactName,
                employee.EmergencyContactPhone,
                employee.ProfilePicturePath
            };
        }
    }
}