using HRManagementSystem.Models;

namespace HRManagementSystem.Services
{
    public interface ILeaveManagementService
    {
        // Leave Balance Management
        Task<LeaveBalance?> GetLeaveBalanceAsync(int employeeId, int leaveTypeId, int year);
        Task<List<LeaveBalance>> GetEmployeeLeaveBalancesAsync(int employeeId, int year);
        Task<bool> InitializeLeaveBalancesAsync(int employeeId, int year);
        Task<bool> UpdateLeaveBalanceAsync(LeaveBalance leaveBalance);

        // Leave Application Management
        Task<LeaveApplication?> GetLeaveApplicationAsync(int leaveApplicationId);
        Task<List<LeaveApplication>> GetEmployeeLeaveApplicationsAsync(int employeeId);
        Task<List<LeaveApplication>> GetPendingLeaveApplicationsForManagerAsync(int managerId);
        Task<List<LeaveApplication>> GetAllLeaveApplicationsAsync();
        Task<bool> ApplyForLeaveAsync(LeaveApplication leaveApplication);
        Task<bool> ApproveLeaveAsync(int leaveApplicationId, int reviewerId, string? comments);
        Task<bool> RejectLeaveAsync(int leaveApplicationId, int reviewerId, string comments);
        Task<bool> CancelLeaveAsync(int leaveApplicationId, int employeeId);

        // Leave Type Management
        Task<List<LeaveType>> GetAllLeaveTypesAsync();
        Task<List<LeaveType>> GetActiveLeaveTypesAsync();
        Task<LeaveType?> GetLeaveTypeAsync(int leaveTypeId);
        Task<bool> CreateLeaveTypeAsync(LeaveType leaveType);
        Task<bool> UpdateLeaveTypeAsync(LeaveType leaveType);

        // Validation
        Task<bool> CanApplyForLeaveAsync(int employeeId, int leaveTypeId, decimal days, DateTime startDate, DateTime endDate);
        Task<decimal> CalculateLeaveDaysAsync(DateTime startDate, DateTime endDate);
    }
}
