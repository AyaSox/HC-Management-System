using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HRManagementSystem.Models
{
    public class LeaveBalance
    {
        public int LeaveBalanceId { get; set; }

        [Required]
        [Display(Name = "Employee")]
        public int EmployeeId { get; set; }
        [ForeignKey("EmployeeId")]
        public Employee Employee { get; set; } = default!;

        [Required]
        [Display(Name = "Leave Type")]
        public int LeaveTypeId { get; set; }
        [ForeignKey("LeaveTypeId")]
        public LeaveType LeaveType { get; set; } = default!;

        [Required]
        [Display(Name = "Year")]
        public int Year { get; set; }

        [Required]
        [Range(0, 365)]
        [Display(Name = "Total Allocated Days")]
        public decimal TotalDays { get; set; }

        [Required]
        [Range(0, 365)]
        [Display(Name = "Used Days")]
        public decimal UsedDays { get; set; }

        [Required]
        [Range(0, 365)]
        [Display(Name = "Pending Days")]
        public decimal PendingDays { get; set; }

        [NotMapped]
        [Display(Name = "Available Days")]
        public decimal AvailableDays => TotalDays - UsedDays - PendingDays;

        [Display(Name = "Carry Forward Days")]
        public decimal CarryForwardDays { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.Now;
        public DateTime? LastModifiedDate { get; set; }
    }
}
