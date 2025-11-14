using Microsoft.EntityFrameworkCore;
using HRManagementSystem.Data;
using HRManagementSystem.Models;

namespace HRManagementSystem.Data
{
    public static class DemoDataSeeder
    {
        public static async Task SeedAsync(AppDbContext context)
        {
            try
            {
                // Only seed if no data exists
                if (await context.Employees.AnyAsync())
                    return;

                // Create departments (updated Human Resources to Human Capital)
                var departments = new[]
                {
                    new Department { Name = "Human Capital" },
                    new Department { Name = "Information Technology" },
                    new Department { Name = "Finance & Accounting" },
                    new Department { Name = "Sales & Marketing" },
                    new Department { Name = "Operations" }
                };

                context.Departments.AddRange(departments);
                await context.SaveChangesAsync();

                // Create sample employees with new fields including Employee Number, Employment Type, and Line Manager
                var employees = new[]
                {
                    new Employee
                    {
                        EmployeeNumber = "EMP001",
                        FullName = "Sarah Johnson",
                        Email = "sarah.johnson@company.co.za",
                        JobTitle = "HR Business Partner",
                        EmploymentType = EmploymentType.Permanent,
                        DateHired = DateTime.Now.AddYears(-3),
                        DateOfBirth = new DateTime(1985, 6, 15),
                        Salary = 650000,
                        Gender = "Female",
                        Status = EmployeeStatus.Active,
                        EmergencyContactName = "Michael Johnson",
                        EmergencyContactPhone = "+27 82 555 0123",
                        DepartmentId = departments[0].DepartmentId,
                        LineManagerId = null // Senior manager, no line manager
                    },
                    new Employee
                    {
                        EmployeeNumber = "EMP002",
                        FullName = "Thabo Mthembu",
                        Email = "thabo.mthembu@company.co.za",
                        JobTitle = "Senior Software Developer",
                        EmploymentType = EmploymentType.Permanent,
                        DateHired = DateTime.Now.AddYears(-2),
                        DateOfBirth = new DateTime(1990, 3, 22),
                        Salary = 750000,
                        Gender = "Male",
                        Status = EmployeeStatus.Active,
                        EmergencyContactName = "Nomsa Mthembu",
                        EmergencyContactPhone = "+27 83 555 0456",
                        DepartmentId = departments[1].DepartmentId,
                        LineManagerId = null // Will be set after Sarah is saved
                    },
                    new Employee
                    {
                        EmployeeNumber = "EMP003",
                        FullName = "Jessica Smith",
                        Email = "jessica.smith@company.co.za",
                        JobTitle = "Financial Analyst",
                        EmploymentType = EmploymentType.Contract,
                        DateHired = DateTime.Now.AddMonths(-8),
                        DateOfBirth = new DateTime(1992, 11, 8),
                        Salary = 580000,
                        Gender = "Female",
                        Status = EmployeeStatus.Active,
                        EmergencyContactName = "David Smith",
                        EmergencyContactPhone = "+27 84 555 0789",
                        DepartmentId = departments[2].DepartmentId,
                        LineManagerId = null // Will be set after Sarah is saved
                    },
                    new Employee
                    {
                        EmployeeNumber = "EMP004",
                        FullName = "Mpho Molefe",
                        Email = "mpho.molefe@company.co.za",
                        JobTitle = "Sales Manager",
                        EmploymentType = EmploymentType.Permanent,
                        DateHired = DateTime.Now.AddYears(-1),
                        DateOfBirth = new DateTime(1988, 9, 30),
                        Salary = 620000,
                        Gender = "Male",
                        Status = EmployeeStatus.OnLeave,
                        EmergencyContactName = "Lerato Molefe",
                        EmergencyContactPhone = "+27 85 555 0321",
                        DepartmentId = departments[3].DepartmentId,
                        LineManagerId = null // Will be set after Sarah is saved
                    },
                    new Employee
                    {
                        EmployeeNumber = "EMP005",
                        FullName = "Lisa Van Der Merwe",
                        Email = "lisa.vandermerwe@company.co.za",
                        JobTitle = "Operations Coordinator",
                        EmploymentType = EmploymentType.Temporary,
                        DateHired = DateTime.Now.AddMonths(-4),
                        DateOfBirth = new DateTime(1993, 12, 25), // Christmas birthday!
                        Salary = 540000,
                        Gender = "Female",
                        Status = EmployeeStatus.Active,
                        EmergencyContactName = "Peter Van Der Merwe",
                        EmergencyContactPhone = "+27 86 555 0654",
                        DepartmentId = departments[4].DepartmentId,
                        LineManagerId = null // Will be set after Sarah is saved
                    }
                };

                context.Employees.AddRange(employees);
                await context.SaveChangesAsync();

                // Now set up line manager relationships (Sarah as the senior manager for others)
                var sarah = employees[0]; // Sarah Johnson
                var thabo = employees[1]; // Thabo Mthembu  
                var jessica = employees[2]; // Jessica Smith
                var mpho = employees[3]; // Mpho Molefe

                // Set Sarah as line manager for Thabo, Jessica, and Mpho
                thabo.LineManagerId = sarah.EmployeeId;
                jessica.LineManagerId = sarah.EmployeeId;
                mpho.LineManagerId = sarah.EmployeeId;
                // Lisa reports to Mpho
                employees[4].LineManagerId = mpho.EmployeeId;

                await context.SaveChangesAsync();

                // Seed Leave Types - South African BCEA Compliant
                if (!await context.LeaveTypes.AnyAsync())
                {
                    var leaveTypes = new[]
                    {
                        new LeaveType
                        {
                            Name = "Annual Leave",
                            Description = "15 working days per year (5-day work week) as per BCEA. Employers may require leave during shutdown periods. Extra day if public holiday falls during leave.",
                            DefaultDaysPerYear = 15, // BCEA: 15 working days for 5-day work week
                            RequiresApproval = true,
                            IsPaid = true,
                            IsActive = true,
                            Color = "bg-primary"
                        },
                        new LeaveType
                        {
                            Name = "Sick Leave",
                            Description = "BCEA Section 22: First 6 months - 1 day per 26 days worked. After 6 months - 30 days for 36-month cycle. Medical certificate required for absences exceeding 2 consecutive days or more than twice in 8 weeks.",
                            DefaultDaysPerYear = 30, // BCEA: 30 days sick leave entitlement (after 6 months)
                            RequiresApproval = false, // Sick leave doesn't require prior approval
                            IsPaid = true,
                            IsActive = true,
                            Color = "bg-danger"
                        },
                        new LeaveType
                        {
                            Name = "Family Responsibility Leave",
                            Description = "3 paid days per year for employees working 4+ days/week after 4 months employment. For death of spouse/partner/parent/child/sibling/grandparent/grandchild or illness of child. Unused days expire at end of cycle.",
                            DefaultDaysPerYear = 3, // BCEA: 3 days per year
                            RequiresApproval = true,
                            IsPaid = true,
                            IsActive = true,
                            Color = "bg-info"
                        },
                        new LeaveType
                        {
                            Name = "Maternity Leave",
                            Description = "4 consecutive months unpaid maternity leave (UIF may provide benefits). May not work 6 weeks before and 6 weeks after birth unless certified fit by doctor.",
                            DefaultDaysPerYear = 120, // ~4 months (approximately 17 weeks)
                            RequiresApproval = true,
                            IsPaid = false, // Unpaid by employer (UIF provides benefits)
                            IsActive = true,
                            Color = "bg-success"
                        },
                        new LeaveType
                        {
                            Name = "Paternity Leave",
                            Description = "10 consecutive days paternity leave for employees after 1 year service. Available to parent who is not entitled to maternity leave (fathers, adoptive parents, commissioning parents).",
                            DefaultDaysPerYear = 10, // BCEA Amendment: 10 days paternity leave
                            RequiresApproval = true,
                            IsPaid = false, // Unpaid (may claim from UIF)
                            IsActive = true,
                            Color = "bg-secondary"
                        },
                        new LeaveType
                        {
                            Name = "Study Leave",
                            Description = "Not mandated by BCEA. Company policy for educational purposes. Typically unpaid unless otherwise agreed.",
                            DefaultDaysPerYear = 5, // Company policy
                            RequiresApproval = true,
                            IsPaid = false,
                            IsActive = true,
                            Color = "bg-warning"
                        },
                        new LeaveType
                        {
                            Name = "Unpaid Leave",
                            Description = "Additional leave beyond statutory entitlements. Requires employer approval. Not paid by employer.",
                            DefaultDaysPerYear = 0, // No default entitlement
                            RequiresApproval = true,
                            IsPaid = false,
                            IsActive = true,
                            Color = "bg-dark"
                        }
                    };

                    context.LeaveTypes.AddRange(leaveTypes);
                    await context.SaveChangesAsync();

                    Console.WriteLine("? South African BCEA-compliant leave types seeded successfully!");
                    Console.WriteLine("  - Annual Leave: 15 days/year (BCEA)");
                    Console.WriteLine("  - Sick Leave: 30 days entitlement (BCEA)");
                    Console.WriteLine("  - Family Responsibility Leave: 3 days/year (BCEA)");
                    Console.WriteLine("  - Maternity Leave: 120 days (BCEA)");
                    Console.WriteLine("  - Paternity Leave: 10 days (BCEA Amendment)");
                    Console.WriteLine("  - Study Leave: 5 days (Company Policy)");
                    Console.WriteLine("  - Unpaid Leave: Available upon approval");
                }

                Console.WriteLine("? Demo data seeded successfully!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"? Demo data seeding error: {ex.Message}");
            }
        }
    }
}