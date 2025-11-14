using System.ComponentModel.DataAnnotations;

namespace HRManagementSystem.Models
{
    public enum StatusChangeRequestStatus
    {
        Pending,
        Approved,
        Rejected
    }

    public class StatusChangeRequest
    {
        public int StatusChangeRequestId { get; set; }
        
        [Required]
        public int EmployeeId { get; set; }
        public Employee Employee { get; set; } = null!;
        
        [Required]
        public EmployeeStatus FromStatus { get; set; }
        
        [Required]
        public EmployeeStatus ToStatus { get; set; }
        
        [Required]
        [StringLength(500)]
        public string Reason { get; set; } = string.Empty;
        
        [Required]
        public string RequestedBy { get; set; } = string.Empty;
        
        [Required]
        public DateTime RequestedDate { get; set; }
        
        public string? ApprovedBy { get; set; }
        
        public DateTime? ApprovedDate { get; set; }
        
        public StatusChangeRequestStatus Status { get; set; } = StatusChangeRequestStatus.Pending;
        
        public string? ApprovalComments { get; set; }
    }
}