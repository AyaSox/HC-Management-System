using HRManagementSystem.Models;

namespace HRManagementSystem.Services
{
    public interface IEmployeeService
    {
        Task<Employee?> GetEmployeeByEmailAsync(string email);
        Task<Employee?> GetEmployeeByIdAsync(int employeeId);
    }
}
