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
    public class DepartmentsApiController : ControllerBase
    {
        private readonly AppDbContext _context;

        public DepartmentsApiController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/DepartmentsApi
        [HttpGet]
        public async Task<ActionResult<IEnumerable<object>>> GetDepartments()
        {
            var departments = await _context.Departments
                .Include(d => d.Employees)
                .Select(d => new
                {
                    d.DepartmentId,
                    d.Name,
                    EmployeeCount = d.Employees.Count
                })
                .ToListAsync();

            return Ok(departments);
        }

        // GET: api/DepartmentsApi/5
        [HttpGet("{id}")]
        public async Task<ActionResult<object>> GetDepartment(int id)
        {
            var department = await _context.Departments
                .Include(d => d.Employees)
                .Where(d => d.DepartmentId == id)
                .Select(d => new
                {
                    d.DepartmentId,
                    d.Name,
                    Employees = d.Employees.Select(e => new
                    {
                        e.EmployeeId,
                        e.FullName,
                        e.Email,
                        e.Salary
                    })
                })
                .FirstOrDefaultAsync();

            if (department == null)
            {
                return NotFound(new { message = "Department not found" });
            }

            return Ok(department);
        }

        // POST: api/DepartmentsApi
        [HttpPost]
        public async Task<ActionResult<object>> CreateDepartment([FromBody] DepartmentCreateDto departmentDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var department = new Department
            {
                Name = departmentDto.Name
            };

            _context.Departments.Add(department);
            await _context.SaveChangesAsync();

            var result = new
            {
                department.DepartmentId,
                department.Name
            };

            return CreatedAtAction(nameof(GetDepartment), new { id = department.DepartmentId }, result);
        }

        // GET: api/DepartmentsApi/5/employees
        [HttpGet("{id}/employees")]
        public async Task<ActionResult<IEnumerable<object>>> GetDepartmentEmployees(int id)
        {
            var department = await _context.Departments
                .Include(d => d.Employees)
                .FirstOrDefaultAsync(d => d.DepartmentId == id);

            if (department == null)
            {
                return NotFound(new { message = "Department not found" });
            }

            var employees = department.Employees.Select(e => new
            {
                e.EmployeeId,
                e.FullName,
                e.Email,
                e.Salary,
                e.DateHired
            });

            return Ok(employees);
        }
    }

    // DTO for creating departments via API
    public class DepartmentCreateDto
    {
        public string Name { get; set; } = string.Empty;
    }
}