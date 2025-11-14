using System.Collections.Generic;

namespace HRManagementSystem.Models.ViewModels
{
    public class EmployeeImportRow
    {
        public string EmployeeNumber { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? JobTitle { get; set; }
        public string? EmploymentType { get; set; }
        public string? DepartmentName { get; set; }
        public string? DateHiredText { get; set; }
        public string? DateOfBirthText { get; set; }
        public string? SalaryText { get; set; }
        public string? Gender { get; set; }
        public string? Status { get; set; }
        public string? EmergencyContactName { get; set; }
        public string? EmergencyContactPhone { get; set; }
        public string? LineManagerName { get; set; }

        // Parsed values
        public System.DateTime? DateHired { get; set; }
        public System.DateTime? DateOfBirth { get; set; }
        public decimal Salary { get; set; }

        public List<string> Errors { get; set; } = new();
    }
}
