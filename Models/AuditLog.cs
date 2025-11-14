using System.ComponentModel.DataAnnotations;

namespace HRManagementSystem.Models
{
    public class AuditLog
    {
        public int AuditLogId { get; set; }

        [Required]
        public string TableName { get; set; } = string.Empty;

        [Required]
        public string Action { get; set; } = string.Empty; // INSERT, UPDATE, DELETE

        public int? RecordId { get; set; }

        public string? OldValues { get; set; }

        public string? NewValues { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;

        [Required]
        public string UserName { get; set; } = string.Empty;

        public DateTime Timestamp { get; set; } = DateTime.Now;

        public string? Changes { get; set; }
    }
}