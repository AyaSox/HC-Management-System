using System.ComponentModel.DataAnnotations;

namespace HRManagementSystem.Models.ViewModels
{
    public class StatusChangeRequestViewModel
    {
        public int EmployeeId { get; set; }
        public string EmployeeName { get; set; } = string.Empty;
        public EmployeeStatus CurrentStatus { get; set; }
        
        [Required]
        [Display(Name = "New Status")]
        public EmployeeStatus NewStatus { get; set; }
        
        [Required]
        [StringLength(500, MinimumLength = 10)]
        [Display(Name = "Reason for Status Change")]
        public string Reason { get; set; } = string.Empty;
        
        public bool RequiresApproval { get; set; }
    }

    public class StatusChangeApprovalViewModel
    {
        public int RequestId { get; set; }
        public string EmployeeName { get; set; } = string.Empty;
        public EmployeeStatus FromStatus { get; set; }
        public EmployeeStatus ToStatus { get; set; }
        public string Reason { get; set; } = string.Empty;
        public string RequestedBy { get; set; } = string.Empty;
        public DateTime RequestedDate { get; set; }
        
        [Required]
        public bool Approved { get; set; }
        
        [StringLength(500)]
        [Display(Name = "Approval Comments")]
        public string? ApprovalComments { get; set; }
    }
}