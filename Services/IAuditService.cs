namespace HRManagementSystem.Services
{
    public interface IAuditService
    {
        Task LogAsync(string description, string entityType, string entityId);
    }
}
