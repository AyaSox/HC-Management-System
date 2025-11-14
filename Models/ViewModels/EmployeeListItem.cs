using System;
using HRManagementSystem.Models;

namespace HRManagementSystem.Models.ViewModels
{
    public class EmployeeListItem
    {
        public int EmployeeId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string? JobTitle { get; set; }
        public string EmploymentType { get; set; } = string.Empty;
        public string? DepartmentName { get; set; }
        public decimal Salary { get; set; }
        public string FormattedSalary { get; set; } = string.Empty;
        public EmployeeStatus Status { get; set; }
        public DateTime DateHired { get; set; }
        public string? ProfilePicturePath { get; set; }
        public int? LineManagerId { get; set; }
        public string? LineManagerFullName { get; set; }
    }
}
