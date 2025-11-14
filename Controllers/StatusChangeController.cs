using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using HRManagementSystem.Data;
using HRManagementSystem.Models;
using HRManagementSystem.Models.ViewModels;
using HRManagementSystem.Services;

namespace HRManagementSystem.Controllers
{
    [Authorize]
    public class StatusChangeController : Controller
    {
        private readonly AppDbContext _context;
        private readonly AuditService _auditService;

        public StatusChangeController(AppDbContext context, AuditService auditService)
        {
            _context = context;
            _auditService = auditService;
        }

        // GET: StatusChange/Request/5
        public async Task<IActionResult> Request(int id)
        {
            var employee = await _context.Employees
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.EmployeeId == id);

            if (employee == null)
                return NotFound();

            var model = new StatusChangeRequestViewModel
            {
                EmployeeId = employee.EmployeeId,
                EmployeeName = employee.FullName,
                CurrentStatus = employee.Status,
                NewStatus = employee.Status,
                RequiresApproval = true // All status changes require approval
            };

            return View(model);
        }

        // POST: StatusChange/Request
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Request(StatusChangeRequestViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var employee = await _context.Employees.FindAsync(model.EmployeeId);
            if (employee == null)
                return NotFound();

            // Check if there's already a pending request for this employee
            var existingRequest = await _context.StatusChangeRequests
                .Where(r => r.EmployeeId == model.EmployeeId && r.Status == StatusChangeRequestStatus.Pending)
                .FirstOrDefaultAsync();

            if (existingRequest != null)
            {
                TempData["Error"] = "There is already a pending status change request for this employee.";
                return RedirectToAction("Details", "Employees", new { id = model.EmployeeId });
            }

            // Create status change request
            var request = new StatusChangeRequest
            {
                EmployeeId = model.EmployeeId,
                FromStatus = employee.Status,
                ToStatus = model.NewStatus,
                Reason = model.Reason,
                RequestedBy = User.Identity?.Name ?? "System",
                RequestedDate = DateTime.Now,
                Status = StatusChangeRequestStatus.Pending
            };

            _context.StatusChangeRequests.Add(request);
            await _context.SaveChangesAsync();

            // Log the request creation
            await _auditService.LogAsync("STATUS_CHANGE_REQUESTED", "StatusChangeRequest", request.StatusChangeRequestId, 
                $"Status change requested for {employee.FullName}: {employee.Status} ? {model.NewStatus}. Reason: {model.Reason}");

            TempData["Success"] = $"Status change request submitted for {employee.FullName}. Awaiting admin approval.";
            return RedirectToAction("Details", "Employees", new { id = model.EmployeeId });
        }

        // GET: StatusChange/Pending (Admin only)
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Pending()
        {
            var pendingRequests = await _context.StatusChangeRequests
                .AsNoTracking()
                .Include(r => r.Employee)
                .Where(r => r.Status == StatusChangeRequestStatus.Pending)
                .OrderBy(r => r.RequestedDate)
                .ToListAsync();

            return View(pendingRequests);
        }

        // GET: StatusChange/Approve/5 (Admin only)
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Approve(int id)
        {
            var request = await _context.StatusChangeRequests
                .AsNoTracking()
                .Include(r => r.Employee)
                .FirstOrDefaultAsync(r => r.StatusChangeRequestId == id);

            if (request == null)
                return NotFound();

            if (request.Status != StatusChangeRequestStatus.Pending)
            {
                TempData["Error"] = "This request has already been processed.";
                return RedirectToAction(nameof(Pending));
            }

            var model = new StatusChangeApprovalViewModel
            {
                RequestId = request.StatusChangeRequestId,
                EmployeeName = request.Employee.FullName,
                FromStatus = request.FromStatus,
                ToStatus = request.ToStatus,
                Reason = request.Reason,
                RequestedBy = request.RequestedBy,
                RequestedDate = request.RequestedDate
            };

            return View(model);
        }

        // POST: StatusChange/Approve
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Approve(StatusChangeApprovalViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var request = await _context.StatusChangeRequests
                .Include(r => r.Employee)
                .FirstOrDefaultAsync(r => r.StatusChangeRequestId == model.RequestId);

            if (request == null)
                return NotFound();

            if (request.Status != StatusChangeRequestStatus.Pending)
            {
                TempData["Error"] = "This request has already been processed.";
                return RedirectToAction(nameof(Pending));
            }

            // Store old employee state for audit
            var oldEmployee = new Employee
            {
                EmployeeId = request.Employee.EmployeeId,
                Status = request.Employee.Status
            };

            // Update request status
            request.Status = model.Approved ? StatusChangeRequestStatus.Approved : StatusChangeRequestStatus.Rejected;
            request.ApprovedBy = User.Identity?.Name ?? "System";
            request.ApprovedDate = DateTime.Now;
            request.ApprovalComments = model.ApprovalComments;

            if (model.Approved)
            {
                // Apply the status change
                request.Employee.Status = request.ToStatus;
                
                // Log the employee change
                await _auditService.LogEmployeeChangeAsync("STATUS_CHANGE_APPROVED", request.Employee, oldEmployee);
            }

            await _context.SaveChangesAsync();

            // Log the approval/rejection
            string action = model.Approved ? "APPROVED" : "REJECTED";
            string logMessage = $"Status change request {action} for {request.Employee.FullName}: {request.FromStatus} ? {request.ToStatus}";
            if (!string.IsNullOrEmpty(model.ApprovalComments))
            {
                logMessage += $". Admin comments: {model.ApprovalComments}";
            }

            await _auditService.LogAsync($"STATUS_CHANGE_{action}", "StatusChangeRequest", request.StatusChangeRequestId, logMessage);

            TempData["Success"] = $"Status change request {(model.Approved ? "approved" : "rejected")} for {request.Employee.FullName}.";
            return RedirectToAction(nameof(Pending));
        }

        // GET: StatusChange/History/5
        public async Task<IActionResult> History(int id)
        {
            var employee = await _context.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.EmployeeId == id);
            if (employee == null)
                return NotFound();

            var requests = await _context.StatusChangeRequests
                .AsNoTracking()
                .Where(r => r.EmployeeId == id)
                .OrderByDescending(r => r.RequestedDate)
                .ToListAsync();

            ViewBag.EmployeeName = employee.FullName;
            return View(requests);
        }
    }
}