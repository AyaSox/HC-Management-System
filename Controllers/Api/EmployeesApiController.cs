using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HRManagementSystem.Data;
using HRManagementSystem.Models;
using Microsoft.AspNetCore.Authorization;

namespace HRManagementSystem.Controllers.Api
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class EmployeesApiController : ControllerBase
    {
        private readonly AppDbContext _context;

        public EmployeesApiController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/EmployeesApi
        [HttpGet]
        public async Task<ActionResult<IEnumerable<object>>> GetEmployees()
        {
            var employees = await _context.Employees
                .Include(e => e.Department)
                .Select(e => new
                {
                    e.EmployeeId,
                    e.FullName,
                    e.Email,
                    e.DateHired,
                    e.Salary,
                    e.Gender,
                    Department = e.Department!.Name,
                    e.ProfilePicturePath
                })
                .ToListAsync();

            return Ok(employees);
        }

        // GET: api/EmployeesApi/5
        [HttpGet("{id}")]
        public async Task<ActionResult<object>> GetEmployee(int id)
        {
            var employee = await _context.Employees
                .Include(e => e.Department)
                .Where(e => e.EmployeeId == id)
                .Select(e => new
                {
                    e.EmployeeId,
                    e.FullName,
                    e.Email,
                    e.DateHired,
                    e.Salary,
                    e.Gender,
                    Department = e.Department!.Name,
                    e.ProfilePicturePath
                })
                .FirstOrDefaultAsync();

            if (employee == null)
            {
                return NotFound(new { message = "Employee not found" });
            }

            return Ok(employee);
        }

        // POST: api/EmployeesApi
        [HttpPost]
        public async Task<ActionResult<object>> CreateEmployee([FromBody] EmployeeCreateDto employeeDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var employee = new Employee
            {
                FullName = employeeDto.FullName,
                Email = employeeDto.Email,
                DateHired = employeeDto.DateHired,
                Salary = employeeDto.Salary,
                Gender = employeeDto.Gender,
                DepartmentId = employeeDto.DepartmentId
            };

            _context.Employees.Add(employee);
            await _context.SaveChangesAsync();

            await _context.Entry(employee).Reference(e => e.Department).LoadAsync();

            var result = new
            {
                employee.EmployeeId,
                employee.FullName,
                employee.Email,
                employee.DateHired,
                employee.Salary,
                employee.Gender,
                Department = employee.Department?.Name
            };

            return CreatedAtAction(nameof(GetEmployee), new { id = employee.EmployeeId }, result);
        }

        // PUT: api/EmployeesApi/5
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateEmployee(int id, [FromBody] EmployeeCreateDto employeeDto)
        {
            var employee = await _context.Employees.FindAsync(id);
            if (employee == null)
            {
                return NotFound(new { message = "Employee not found" });
            }

            employee.FullName = employeeDto.FullName;
            employee.Email = employeeDto.Email;
            employee.DateHired = employeeDto.DateHired;
            employee.Salary = employeeDto.Salary;
            employee.Gender = employeeDto.Gender;
            employee.DepartmentId = employeeDto.DepartmentId;

            try
            {
                await _context.SaveChangesAsync();
                return Ok(new { message = "Employee updated successfully" });
            }
            catch (DbUpdateConcurrencyException)
            {
                return Conflict(new { message = "Employee was updated by another user" });
            }
        }

        // DELETE: api/EmployeesApi/5
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteEmployee(int id)
        {
            var employee = await _context.Employees.FindAsync(id);
            if (employee == null)
            {
                return NotFound(new { message = "Employee not found" });
            }

            _context.Employees.Remove(employee);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Employee deleted successfully" });
        }

        // GET: api/EmployeesApi/search?term=john
        [HttpGet("search")]
        public async Task<ActionResult<IEnumerable<object>>> SearchEmployees([FromQuery] string term)
        {
            if (string.IsNullOrWhiteSpace(term))
            {
                return BadRequest(new { message = "Search term is required" });
            }

            var employees = await _context.Employees
                .Include(e => e.Department)
                .Where(e => e.FullName.Contains(term) || e.Email.Contains(term))
                .Select(e => new
                {
                    e.EmployeeId,
                    e.FullName,
                    e.Email,
                    Department = e.Department!.Name,
                    e.Salary
                })
                .ToListAsync();

            return Ok(employees);
        }
    }

    // DTO for creating/updating employees via API
    public class EmployeeCreateDto
    {
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public DateTime DateHired { get; set; }
        public decimal Salary { get; set; }
        public string? Gender { get; set; }
        public int DepartmentId { get; set; }
    }
}