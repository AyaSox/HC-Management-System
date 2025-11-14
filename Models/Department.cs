using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace HRManagementSystem.Models
{
    public class Department
    {
        public int DepartmentId { get; set; }

        [Required, StringLength(100)]
        public string Name { get; set; } = string.Empty;

        public List<Employee> Employees { get; set; } = new();
    }
}