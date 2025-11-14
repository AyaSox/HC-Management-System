using HRManagementSystem.Services;
using HRManagement.Shared.Models;
using HRManagement.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace HRManagementSystem.Controllers
{
    [Authorize]
    public class LeaveController : Controller
    {
        private readonly IESSLeaveApiClient _essApiClient;
        private readonly IEmployeeService _employeeService;

        public LeaveController(IESSLeaveApiClient essApiClient, IEmployeeService employeeService)
        {
            _essApiClient = essApiClient;
            _employeeService = employeeService;
        }

        // GET: Leave/MyLeave
        public async Task<IActionResult> MyLeave()
        {
            var userEmail = User.FindFirstValue(ClaimTypes.Email);
            if (userEmail == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var employee = await _employeeService.GetEmployeeByEmailAsync(userEmail);
            if (employee == null)
            {
                TempData["Error"] = "Employee profile not found. Please contact HR.";
                return RedirectToAction("Index", "Home");
            }

            var currentYear = DateTime.Now.Year;
            var balancesResponse = await _essApiClient.GetEmployeeLeaveBalancesAsync(employee.EmployeeId, currentYear);
            var applicationsResponse = await _essApiClient.GetEmployeeLeaveApplicationsAsync(employee.EmployeeId);

            var leaveBalances = balancesResponse.Success ? balancesResponse.Data ?? new List<LeaveBalance>() : new List<LeaveBalance>();
            var leaveApplications = applicationsResponse.Success ? applicationsResponse.Data ?? new List<LeaveApplication>() : new List<LeaveApplication>();

            ViewBag.LeaveBalances = leaveBalances;
            ViewBag.Employee = employee;
            ViewBag.CurrentYear = currentYear;

            return View(leaveApplications);
        }

        // GET: Leave/Apply
        public async Task<IActionResult> Apply()
        {
            var userEmail = User.FindFirstValue(ClaimTypes.Email);
            if (userEmail == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var employee = await _employeeService.GetEmployeeByEmailAsync(userEmail);
            if (employee == null)
            {
                TempData["Error"] = "Employee profile not found. Please contact HR.";
                return RedirectToAction("Index", "Home");
            }

            var currentYear = DateTime.Now.Year;
            var balancesResponse = await _essApiClient.GetEmployeeLeaveBalancesAsync(employee.EmployeeId, currentYear);
            var leaveBalances = balancesResponse.Success ? balancesResponse.Data ?? new List<LeaveBalance>() : new List<LeaveBalance>();
            
            // Initialize leave balances if empty
            if (!leaveBalances.Any())
            {
                await _essApiClient.InitializeLeaveBalancesAsync(employee.EmployeeId, currentYear);
                balancesResponse = await _essApiClient.GetEmployeeLeaveBalancesAsync(employee.EmployeeId, currentYear);
                leaveBalances = balancesResponse.Success ? balancesResponse.Data ?? new List<LeaveBalance>() : new List<LeaveBalance>();
            }

            var leaveTypesResponse = await _essApiClient.GetLeaveTypesAsync();
            ViewBag.LeaveTypes = leaveTypesResponse.Success ? leaveTypesResponse.Data ?? new List<LeaveType>() : new List<LeaveType>();
            ViewBag.LeaveBalances = leaveBalances;
            ViewBag.Employee = employee;

            return View(new LeaveApplication
            {
                EmployeeId = employee.EmployeeId,
                StartDate = DateTime.Today,
                EndDate = DateTime.Today
            });
        }

        // POST: Leave/Apply
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Apply(LeaveApplication model, IFormFile? supportingDocument)
        {
            var userEmail = User.FindFirstValue(ClaimTypes.Email);
            if (userEmail == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var employee = await _employeeService.GetEmployeeByEmailAsync(userEmail);
            if (employee == null)
            {
                TempData["Error"] = "Employee profile not found.";
                return RedirectToAction("Index", "Home");
            }

            model.EmployeeId = employee.EmployeeId;

            // Handle file upload
            if (supportingDocument != null && supportingDocument.Length > 0)
            {
                var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "leave-documents");
                Directory.CreateDirectory(uploadsFolder);

                var uniqueFileName = $"{Guid.NewGuid()}_{Path.GetFileName(supportingDocument.FileName)}";
                var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await supportingDocument.CopyToAsync(fileStream);
                }

                model.SupportingDocumentPath = $"/uploads/leave-documents/{uniqueFileName}";
            }

                var leaveRequest = new LeaveApplicationRequest
                {
                    EmployeeId = model.EmployeeId,
                    LeaveTypeId = model.LeaveTypeId,
                    StartDate = model.StartDate,
                    EndDate = model.EndDate,
                    Reason = model.Reason,
                    ContactDuringLeave = model.ContactDuringLeave,
                    SupportingDocumentPath = model.SupportingDocumentPath
                };

                var response = await _essApiClient.CreateLeaveApplicationAsync(leaveRequest);

                if (response.Success)
                {
                    TempData["Success"] = "Leave application submitted successfully!";
                    return RedirectToAction(nameof(MyLeave));
                }

                TempData["Error"] = response.Message ?? "Failed to submit leave application. Please check your leave balance and try again.";
                
                var leaveTypesResponse = await _essApiClient.GetLeaveTypesAsync();
                ViewBag.LeaveTypes = leaveTypesResponse.Success ? leaveTypesResponse.Data ?? new List<LeaveType>() : new List<LeaveType>();
                
                var balancesResponse = await _essApiClient.GetEmployeeLeaveBalancesAsync(employee.EmployeeId, DateTime.Now.Year);
                ViewBag.LeaveBalances = balancesResponse.Success ? balancesResponse.Data ?? new List<LeaveBalance>() : new List<LeaveBalance>();
            ViewBag.Employee = employee;

            return View(model);
        }

        // GET: Leave/Details/5
        public async Task<IActionResult> Details(int id)
        {
            // For now, redirect to MyLeave since we need to implement the API endpoint
            // This would require implementing GetLeaveApplication in the API
            TempData["Info"] = "Leave application details view is not yet implemented in the API architecture.";
            return RedirectToAction(nameof(MyLeave));
        }

        // POST: Leave/Cancel/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(int id)
        {
            var userEmail = User.FindFirstValue(ClaimTypes.Email);
            var employee = await _employeeService.GetEmployeeByEmailAsync(userEmail!);

            if (employee == null)
            {
                TempData["Error"] = "Employee profile not found.";
                return RedirectToAction("Index", "Home");
            }

            // This would need to call the ESS API cancel endpoint
            // For now, show a message
            TempData["Info"] = "Leave cancellation via API is not yet implemented.";
            return RedirectToAction(nameof(MyLeave));
        }

        // GET: Leave/ManagerDashboard
        public async Task<IActionResult> ManagerDashboard()
        {
            var userEmail = User.FindFirstValue(ClaimTypes.Email);
            var employee = await _employeeService.GetEmployeeByEmailAsync(userEmail!);

            if (employee == null)
            {
                TempData["Error"] = "Employee profile not found.";
                return RedirectToAction("Index", "Home");
            }

            var pendingResponse = await _essApiClient.GetPendingLeaveApplicationsForManagerAsync(employee.EmployeeId);
            var pendingLeaves = pendingResponse.Success ? pendingResponse.Data ?? new List<LeaveApplication>() : new List<LeaveApplication>();
            
            ViewBag.Manager = employee;

            return View(pendingLeaves);
        }

        // POST: Leave/Approve/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(int id, string? comments)
        {
            var userEmail = User.FindFirstValue(ClaimTypes.Email);
            var employee = await _employeeService.GetEmployeeByEmailAsync(userEmail!);

            if (employee == null)
            {
                TempData["Error"] = "Employee profile not found.";
                return RedirectToAction("Index", "Home");
            }

            var approvalRequest = new LeaveApprovalRequest
            {
                LeaveApplicationId = id,
                ReviewerId = employee.EmployeeId,
                Comments = comments,
                IsApproved = true
            };

            var response = await _essApiClient.ApproveLeaveApplicationAsync(id, approvalRequest);

            if (response.Success)
            {
                TempData["Success"] = "Leave application approved successfully.";
            }
            else
            {
                TempData["Error"] = response.Message ?? "Failed to approve leave application.";
            }

            return RedirectToAction(nameof(ManagerDashboard));
        }

        // POST: Leave/Reject
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(int id, string comments)
        {
            var userEmail = User.FindFirstValue(ClaimTypes.Email);
            var employee = await _employeeService.GetEmployeeByEmailAsync(userEmail!);

            if (employee == null)
            {
                TempData["Error"] = "Employee profile not found.";
                return RedirectToAction("Index", "Home");
            }

            if (string.IsNullOrWhiteSpace(comments))
            {
                TempData["Error"] = "Please provide a reason for rejection.";
                return RedirectToAction(nameof(ManagerDashboard));
            }

            var rejectionRequest = new LeaveApprovalRequest
            {
                LeaveApplicationId = id,
                ReviewerId = employee.EmployeeId,
                Comments = comments,
                IsApproved = false
            };

            var response = await _essApiClient.ApproveLeaveApplicationAsync(id, rejectionRequest);

            if (response.Success)
            {
                TempData["Success"] = "Leave application rejected.";
            }
            else
            {
                TempData["Error"] = response.Message ?? "Failed to reject leave application.";
            }

            return RedirectToAction(nameof(ManagerDashboard));
        }

        // GET: Leave/HRDashboard (Admin only)
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> HRDashboard()
        {
            // Keep leave workflows in ESS. Surface info/link here and redirect to HRMS Home.
            TempData["Info"] = "HR Leave Management is handled in the ESS Leave System. Use dashboards and reports in HRMS; submit/approve leave in ESS.";
            return RedirectToAction("Index", "Home");
        }

        // Placeholder action for admin functions
        [Authorize(Roles = "Admin")]
        public IActionResult LeaveTypes()
        {
            TempData["Info"] = "Leave Types are managed via the ESS API. Open ESS Leave System to create or edit types.";
            return RedirectToAction("Index", "Home");
        }
    }
}
