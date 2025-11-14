using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Globalization;
using Microsoft.AspNetCore.Http;

namespace HRManagementSystem.Models
{
    public enum EmployeeStatus
    {
        Active,
        OnLeave,
        Inactive
    }

    public enum EmploymentType
    {
        Permanent,
        Contract,
        Temporary,
        Intern,
        Graduate,
        Consultant
    }

    public class Employee
    {
        public int EmployeeId { get; set; }

        [Required(ErrorMessage = "Employee Number is required")]
        [StringLength(20)]
        [Display(Name = "Employee Number")]
        public string EmployeeNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "Full Name is required"), StringLength(100)]
        [Display(Name = "Full Name")]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email is required"), EmailAddress(ErrorMessage = "Please enter a valid email address")]
        public string Email { get; set; } = string.Empty;

        [StringLength(100)]
        [Display(Name = "Job Title")]
        public string? JobTitle { get; set; }

        [Display(Name = "Employment Type")]
        public EmploymentType EmploymentType { get; set; } = EmploymentType.Permanent;

        [DataType(DataType.Date)]
        [Display(Name = "Date Hired")]
        public DateTime DateHired { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "Date of Birth")]
        public DateTime? DateOfBirth { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "Salary must be a positive number")]
        [Display(Name = "Annual Salary")]
        public decimal Salary { get; set; }

        [StringLength(20)]
        public string? Gender { get; set; }  // e.g., "Male", "Female", "Other"

        [Display(Name = "Status")]
        public EmployeeStatus Status { get; set; } = EmployeeStatus.Active;

        [StringLength(100)]
        [Display(Name = "Emergency Contact Name")]
        public string? EmergencyContactName { get; set; }

        [Phone]
        [Display(Name = "Emergency Contact Phone")]
        public string? EmergencyContactPhone { get; set; }

        // Line Manager relationship (self-referencing foreign key)
        [Display(Name = "Line Manager")]
        public int? LineManagerId { get; set; }

        [ForeignKey("LineManagerId")]
        public Employee? LineManager { get; set; }

        // Navigation property for employees reporting to this person
        public ICollection<Employee> DirectReports { get; set; } = new List<Employee>();

        // Department relationship
        [Display(Name = "Department")]
        public int DepartmentId { get; set; }
        public Department? Department { get; set; }

        // Profile picture
        public string? ProfilePicturePath { get; set; }

        [NotMapped]
        public IFormFile? ProfilePicture { get; set; }

        // Soft delete
        public bool IsDeleted { get; set; }

        // Calculated property for age
        [NotMapped]
        public int? Age
        {
            get
            {
                if (!DateOfBirth.HasValue) return null;
                var today = DateTime.Today;
                var age = today.Year - DateOfBirth.Value.Year;
                if (DateOfBirth.Value.Date > today.AddYears(-age)) age--;
                return age;
            }
        }

        // Check if birthday is coming up (next 30 days)
        [NotMapped]
        public bool HasUpcomingBirthday
        {
            get
            {
                if (!DateOfBirth.HasValue) return false;
                var today = DateTime.Today;
                var nextBirthday = new DateTime(today.Year, DateOfBirth.Value.Month, DateOfBirth.Value.Day);
                if (nextBirthday < today)
                    nextBirthday = nextBirthday.AddYears(1);
                
                return (nextBirthday - today).Days <= 30;
            }
        }

        // Helper property to format salary in South African Rand
        [NotMapped]
        public string FormattedSalary 
        { 
            get => Salary.ToString("C2", CultureInfo.CurrentCulture);
        }

        // Helper property to calculate length of service
        [NotMapped]
        public string LengthOfService
        {
            get
            {
                var today = DateTime.Today;
                var years = today.Year - DateHired.Year;
                var months = today.Month - DateHired.Month;
                var days = today.Day - DateHired.Day;

                // Adjust for negative days
                if (days < 0)
                {
                    months--;
                    days += DateTime.DaysInMonth(today.AddMonths(-1).Year, today.AddMonths(-1).Month);
                }

                // Adjust for negative months
                if (months < 0)
                {
                    years--;
                    months += 12;
                }

                if (years > 0)
                {
                    return years == 1 ? "1 year" : $"{years} years";
                }
                else if (months > 0)
                {
                    return months == 1 ? "1 month" : $"{months} months";
                }
                else
                {
                    return days == 1 ? "1 day" : $"{days} days";
                }
            }
        }

        // Helper property to check if anniversary is coming up (next 30 days)
        [NotMapped]
        public bool HasUpcomingAnniversary
        {
            get
            {
                var today = DateTime.Today;
                var thisYearAnniversary = new DateTime(today.Year, DateHired.Month, DateHired.Day);
                if (thisYearAnniversary < today) thisYearAnniversary = thisYearAnniversary.AddYears(1);
                return (thisYearAnniversary - today).Days <= 30 && (thisYearAnniversary - today).Days >= 0;
            }
        }

        // Helper property to get years of service for anniversaries
        [NotMapped]
        public int YearsOfService => DateTime.Today.Year - DateHired.Year;
    }
}