using HRManagementSystem.Data;
using HRManagementSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace HRManagementSystem.Services
{
    public class LeaveManagementService : ILeaveManagementService
    {
        private readonly AppDbContext _context;
        private readonly IAuditService _auditService;

        public LeaveManagementService(AppDbContext context, IAuditService auditService)
        {
            _context = context;
            _auditService = auditService;
        }

        // Leave Balance Management
        public async Task<LeaveBalance?> GetLeaveBalanceAsync(int employeeId, int leaveTypeId, int year)
        {
            return await _context.LeaveBalances
                .Include(lb => lb.Employee)
                .Include(lb => lb.LeaveType)
                .FirstOrDefaultAsync(lb => lb.EmployeeId == employeeId 
                    && lb.LeaveTypeId == leaveTypeId 
                    && lb.Year == year);
        }

        public async Task<List<LeaveBalance>> GetEmployeeLeaveBalancesAsync(int employeeId, int year)
        {
            return await _context.LeaveBalances
                .Include(lb => lb.LeaveType)
                .Where(lb => lb.EmployeeId == employeeId && lb.Year == year)
                .OrderBy(lb => lb.LeaveType.Name)
                .ToListAsync();
        }

        public async Task<bool> InitializeLeaveBalancesAsync(int employeeId, int year)
        {
            try
            {
                var activeLeaveTypes = await GetActiveLeaveTypesAsync();
                var employee = await _context.Employees.FindAsync(employeeId);
                
                if (employee == null) return false;

                foreach (var leaveType in activeLeaveTypes)
                {
                    var existingBalance = await GetLeaveBalanceAsync(employeeId, leaveType.LeaveTypeId, year);
                    
                    if (existingBalance == null)
                    {
                        var leaveBalance = new LeaveBalance
                        {
                            EmployeeId = employeeId,
                            LeaveTypeId = leaveType.LeaveTypeId,
                            Year = year,
                            TotalDays = leaveType.DefaultDaysPerYear,
                            UsedDays = 0,
                            PendingDays = 0,
                            CarryForwardDays = 0,
                            CreatedDate = DateTime.Now
                        };

                        _context.LeaveBalances.Add(leaveBalance);
                    }
                }

                await _context.SaveChangesAsync();
                await _auditService.LogAsync($"Initialized leave balances for {employee.FullName} for year {year}", "LeaveBalance", employeeId.ToString());
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> UpdateLeaveBalanceAsync(LeaveBalance leaveBalance)
        {
            try
            {
                leaveBalance.LastModifiedDate = DateTime.Now;
                _context.LeaveBalances.Update(leaveBalance);
                await _context.SaveChangesAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        // Leave Application Management
        public async Task<LeaveApplication?> GetLeaveApplicationAsync(int leaveApplicationId)
        {
            return await _context.LeaveApplications
                .Include(la => la.Employee)
                    .ThenInclude(e => e.Department)
                .Include(la => la.Employee.LineManager)
                .Include(la => la.LeaveType)
                .Include(la => la.ReviewedBy)
                .FirstOrDefaultAsync(la => la.LeaveApplicationId == leaveApplicationId);
        }

        public async Task<List<LeaveApplication>> GetEmployeeLeaveApplicationsAsync(int employeeId)
        {
            return await _context.LeaveApplications
                .Include(la => la.LeaveType)
                .Include(la => la.ReviewedBy)
                .Where(la => la.EmployeeId == employeeId)
                .OrderByDescending(la => la.AppliedDate)
                .ToListAsync();
        }

        public async Task<List<LeaveApplication>> GetPendingLeaveApplicationsForManagerAsync(int managerId)
        {
            return await _context.LeaveApplications
                .Include(la => la.Employee)
                    .ThenInclude(e => e.Department)
                .Include(la => la.LeaveType)
                .Where(la => la.Status == LeaveStatus.Pending 
                    && la.Employee.LineManagerId == managerId)
                .OrderBy(la => la.AppliedDate)
                .ToListAsync();
        }

        public async Task<List<LeaveApplication>> GetAllLeaveApplicationsAsync()
        {
            return await _context.LeaveApplications
                .Include(la => la.Employee)
                    .ThenInclude(e => e.Department)
                .Include(la => la.LeaveType)
                .Include(la => la.ReviewedBy)
                .OrderByDescending(la => la.AppliedDate)
                .ToListAsync();
        }

        public async Task<bool> ApplyForLeaveAsync(LeaveApplication leaveApplication)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Validate leave application
                var canApply = await CanApplyForLeaveAsync(
                    leaveApplication.EmployeeId,
                    leaveApplication.LeaveTypeId,
                    leaveApplication.TotalDays,
                    leaveApplication.StartDate,
                    leaveApplication.EndDate
                );

                if (!canApply)
                {
                    return false;
                }

                // Calculate total days
                leaveApplication.TotalDays = await CalculateLeaveDaysAsync(
                    leaveApplication.StartDate,
                    leaveApplication.EndDate
                );

                leaveApplication.Status = LeaveStatus.Pending;
                leaveApplication.AppliedDate = DateTime.Now;

                _context.LeaveApplications.Add(leaveApplication);
                await _context.SaveChangesAsync();

                // Update pending days in leave balance
                var leaveBalance = await GetLeaveBalanceAsync(
                    leaveApplication.EmployeeId,
                    leaveApplication.LeaveTypeId,
                    leaveApplication.StartDate.Year
                );

                if (leaveBalance != null)
                {
                    leaveBalance.PendingDays += leaveApplication.TotalDays;
                    await UpdateLeaveBalanceAsync(leaveBalance);
                }

                var employee = await _context.Employees.FindAsync(leaveApplication.EmployeeId);
                await _auditService.LogAsync(
                    $"{employee?.FullName} applied for leave from {leaveApplication.StartDate:dd/MM/yyyy} to {leaveApplication.EndDate:dd/MM/yyyy}",
                    "LeaveApplication",
                    leaveApplication.LeaveApplicationId.ToString()
                );

                await transaction.CommitAsync();
                return true;
            }
            catch
            {
                await transaction.RollbackAsync();
                return false;
            }
        }

        public async Task<bool> ApproveLeaveAsync(int leaveApplicationId, int reviewerId, string? comments)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var leaveApplication = await GetLeaveApplicationAsync(leaveApplicationId);
                if (leaveApplication == null || leaveApplication.Status != LeaveStatus.Pending)
                {
                    return false;
                }

                leaveApplication.Status = LeaveStatus.Approved;
                leaveApplication.ReviewedById = reviewerId;
                leaveApplication.ReviewedDate = DateTime.Now;
                leaveApplication.ReviewComments = comments;

                _context.LeaveApplications.Update(leaveApplication);

                // Update leave balance: move from pending to used
                var leaveBalance = await GetLeaveBalanceAsync(
                    leaveApplication.EmployeeId,
                    leaveApplication.LeaveTypeId,
                    leaveApplication.StartDate.Year
                );

                if (leaveBalance != null)
                {
                    leaveBalance.PendingDays -= leaveApplication.TotalDays;
                    leaveBalance.UsedDays += leaveApplication.TotalDays;
                    await UpdateLeaveBalanceAsync(leaveBalance);
                }

                await _context.SaveChangesAsync();

                var reviewer = await _context.Employees.FindAsync(reviewerId);
                await _auditService.LogAsync(
                    $"{reviewer?.FullName} approved leave for {leaveApplication.Employee.FullName} from {leaveApplication.StartDate:dd/MM/yyyy} to {leaveApplication.EndDate:dd/MM/yyyy}",
                    "LeaveApplication",
                    leaveApplicationId.ToString()
                );

                await transaction.CommitAsync();
                return true;
            }
            catch
            {
                await transaction.RollbackAsync();
                return false;
            }
        }

        public async Task<bool> RejectLeaveAsync(int leaveApplicationId, int reviewerId, string comments)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var leaveApplication = await GetLeaveApplicationAsync(leaveApplicationId);
                if (leaveApplication == null || leaveApplication.Status != LeaveStatus.Pending)
                {
                    return false;
                }

                leaveApplication.Status = LeaveStatus.Rejected;
                leaveApplication.ReviewedById = reviewerId;
                leaveApplication.ReviewedDate = DateTime.Now;
                leaveApplication.ReviewComments = comments;

                _context.LeaveApplications.Update(leaveApplication);

                // Update leave balance: remove from pending
                var leaveBalance = await GetLeaveBalanceAsync(
                    leaveApplication.EmployeeId,
                    leaveApplication.LeaveTypeId,
                    leaveApplication.StartDate.Year
                );

                if (leaveBalance != null)
                {
                    leaveBalance.PendingDays -= leaveApplication.TotalDays;
                    await UpdateLeaveBalanceAsync(leaveBalance);
                }

                await _context.SaveChangesAsync();

                var reviewer = await _context.Employees.FindAsync(reviewerId);
                await _auditService.LogAsync(
                    $"{reviewer?.FullName} rejected leave for {leaveApplication.Employee.FullName}. Reason: {comments}",
                    "LeaveApplication",
                    leaveApplicationId.ToString()
                );

                await transaction.CommitAsync();
                return true;
            }
            catch
            {
                await transaction.RollbackAsync();
                return false;
            }
        }

        public async Task<bool> CancelLeaveAsync(int leaveApplicationId, int employeeId)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var leaveApplication = await GetLeaveApplicationAsync(leaveApplicationId);
                if (leaveApplication == null || leaveApplication.EmployeeId != employeeId)
                {
                    return false;
                }

                if (!leaveApplication.CanBeCancelled)
                {
                    return false;
                }

                var previousStatus = leaveApplication.Status;
                leaveApplication.Status = LeaveStatus.Cancelled;
                leaveApplication.ReviewedDate = DateTime.Now;

                _context.LeaveApplications.Update(leaveApplication);

                // Update leave balance
                var leaveBalance = await GetLeaveBalanceAsync(
                    leaveApplication.EmployeeId,
                    leaveApplication.LeaveTypeId,
                    leaveApplication.StartDate.Year
                );

                if (leaveBalance != null)
                {
                    if (previousStatus == LeaveStatus.Pending)
                    {
                        leaveBalance.PendingDays -= leaveApplication.TotalDays;
                    }
                    else if (previousStatus == LeaveStatus.Approved)
                    {
                        leaveBalance.UsedDays -= leaveApplication.TotalDays;
                    }
                    await UpdateLeaveBalanceAsync(leaveBalance);
                }

                await _context.SaveChangesAsync();

                await _auditService.LogAsync(
                    $"{leaveApplication.Employee.FullName} cancelled leave from {leaveApplication.StartDate:dd/MM/yyyy} to {leaveApplication.EndDate:dd/MM/yyyy}",
                    "LeaveApplication",
                    leaveApplicationId.ToString()
                );

                await transaction.CommitAsync();
                return true;
            }
            catch
            {
                await transaction.RollbackAsync();
                return false;
            }
        }

        // Leave Type Management
        public async Task<List<LeaveType>> GetAllLeaveTypesAsync()
        {
            return await _context.LeaveTypes
                .OrderBy(lt => lt.Name)
                .ToListAsync();
        }

        public async Task<List<LeaveType>> GetActiveLeaveTypesAsync()
        {
            return await _context.LeaveTypes
                .Where(lt => lt.IsActive)
                .OrderBy(lt => lt.Name)
                .ToListAsync();
        }

        public async Task<LeaveType?> GetLeaveTypeAsync(int leaveTypeId)
        {
            return await _context.LeaveTypes.FindAsync(leaveTypeId);
        }

        public async Task<bool> CreateLeaveTypeAsync(LeaveType leaveType)
        {
            try
            {
                _context.LeaveTypes.Add(leaveType);
                await _context.SaveChangesAsync();
                await _auditService.LogAsync($"Created leave type: {leaveType.Name}", "LeaveType", leaveType.LeaveTypeId.ToString());
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> UpdateLeaveTypeAsync(LeaveType leaveType)
        {
            try
            {
                _context.LeaveTypes.Update(leaveType);
                await _context.SaveChangesAsync();
                await _auditService.LogAsync($"Updated leave type: {leaveType.Name}", "LeaveType", leaveType.LeaveTypeId.ToString());
                return true;
            }
            catch
            {
                return false;
            }
        }

        // Validation
        public async Task<bool> CanApplyForLeaveAsync(int employeeId, int leaveTypeId, decimal days, DateTime startDate, DateTime endDate)
        {
            try
            {
                // Check if employee exists and is active
                var employee = await _context.Employees.FindAsync(employeeId);
                if (employee == null || employee.Status != EmployeeStatus.Active)
                {
                    return false;
                }

                // Check if leave type exists and is active
                var leaveType = await GetLeaveTypeAsync(leaveTypeId);
                if (leaveType == null || !leaveType.IsActive)
                {
                    return false;
                }

                // Check if dates are valid
                if (startDate > endDate || startDate < DateTime.Today)
                {
                    return false;
                }

                // Check leave balance
                var leaveBalance = await GetLeaveBalanceAsync(employeeId, leaveTypeId, startDate.Year);
                if (leaveBalance == null)
                {
                    // Initialize leave balance if not exists
                    await InitializeLeaveBalancesAsync(employeeId, startDate.Year);
                    leaveBalance = await GetLeaveBalanceAsync(employeeId, leaveTypeId, startDate.Year);
                }

                if (leaveBalance == null || leaveBalance.AvailableDays < days)
                {
                    return false;
                }

                // Check for overlapping leave applications
                var overlapping = await _context.LeaveApplications
                    .Where(la => la.EmployeeId == employeeId
                        && (la.Status == LeaveStatus.Pending || la.Status == LeaveStatus.Approved)
                        && ((la.StartDate <= endDate && la.EndDate >= startDate)))
                    .AnyAsync();

                return !overlapping;
            }
            catch
            {
                return false;
            }
        }

        public async Task<decimal> CalculateLeaveDaysAsync(DateTime startDate, DateTime endDate)
        {
            decimal totalDays = 0;
            var currentDate = startDate;

            while (currentDate <= endDate)
            {
                // Skip weekends (Saturday and Sunday)
                if (currentDate.DayOfWeek != DayOfWeek.Saturday && currentDate.DayOfWeek != DayOfWeek.Sunday)
                {
                    totalDays++;
                }
                currentDate = currentDate.AddDays(1);
            }

            return totalDays;
        }
    }
}
