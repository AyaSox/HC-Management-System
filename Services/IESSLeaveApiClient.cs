using HRManagement.Shared.Models;
using HRManagement.Shared.DTOs;

namespace HRManagementSystem.Services
{
    public interface IESSLeaveApiClient
    {
        Task<ApiResponse<List<LeaveBalance>>> GetEmployeeLeaveBalancesAsync(int employeeId, int year);
        Task<ApiResponse<List<LeaveApplication>>> GetEmployeeLeaveApplicationsAsync(int employeeId);
        Task<ApiResponse<List<LeaveType>>> GetLeaveTypesAsync();
        Task<ApiResponse<LeaveApplication>> CreateLeaveApplicationAsync(LeaveApplicationRequest request);
        Task<ApiResponse<List<LeaveBalance>>> InitializeLeaveBalancesAsync(int employeeId, int year);
    }
}
