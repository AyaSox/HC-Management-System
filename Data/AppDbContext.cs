using Microsoft.EntityFrameworkCore;
using HRManagementSystem.Models;
using System.ComponentModel.DataAnnotations;

namespace HRManagementSystem.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Employee> Employees { get; set; } = default!;
        public DbSet<Department> Departments { get; set; } = default!;
        public DbSet<AuditLog> AuditLogs { get; set; } = default!;
        public DbSet<StatusChangeRequest> StatusChangeRequests { get; set; } = default!;
        
        // Leave Management
        public DbSet<LeaveType> LeaveTypes { get; set; } = default!;
        public DbSet<LeaveBalance> LeaveBalances { get; set; } = default!;
        public DbSet<LeaveApplication> LeaveApplications { get; set; } = default!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Global query filter for soft-deleted employees
            modelBuilder.Entity<Employee>().HasQueryFilter(e => !e.IsDeleted);

            // Configure self-referencing relationship for Line Manager
            modelBuilder.Entity<Employee>()
                .HasOne(e => e.LineManager)
                .WithMany(e => e.DirectReports)
                .HasForeignKey(e => e.LineManagerId)
                .OnDelete(DeleteBehavior.Restrict); // Prevent cascading deletes

            // Configure Department relationship
            modelBuilder.Entity<Employee>()
                .HasOne(e => e.Department)
                .WithMany(d => d.Employees)
                .HasForeignKey(e => e.DepartmentId)
                .OnDelete(DeleteBehavior.Restrict);

            // Unique indexes
            modelBuilder.Entity<Employee>()
                .HasIndex(e => e.EmployeeNumber)
                .IsUnique();
            modelBuilder.Entity<Employee>()
                .HasIndex(e => e.Email)
                .IsUnique();
            modelBuilder.Entity<Department>()
                .HasIndex(d => d.Name)
                .IsUnique();

            // Audit default timestamp (note: for SQLite, this will be set from app code)
            modelBuilder.Entity<AuditLog>()
                .Property(a => a.Timestamp)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            // StatusChangeRequest relationships
            modelBuilder.Entity<StatusChangeRequest>()
                .HasOne(s => s.Employee)
                .WithMany()
                .HasForeignKey(s => s.EmployeeId)
                .OnDelete(DeleteBehavior.Cascade);

            // Index for better performance
            modelBuilder.Entity<StatusChangeRequest>()
                .HasIndex(s => s.Status);

            modelBuilder.Entity<StatusChangeRequest>()
                .HasIndex(s => s.RequestedDate);

            // Leave Management Configuration
            // LeaveBalance relationships
            modelBuilder.Entity<LeaveBalance>()
                .HasOne(lb => lb.Employee)
                .WithMany()
                .HasForeignKey(lb => lb.EmployeeId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<LeaveBalance>()
                .HasOne(lb => lb.LeaveType)
                .WithMany(lt => lt.LeaveBalances)
                .HasForeignKey(lb => lb.LeaveTypeId)
                .OnDelete(DeleteBehavior.Restrict);

            // Unique constraint for employee, leave type, and year
            modelBuilder.Entity<LeaveBalance>()
                .HasIndex(lb => new { lb.EmployeeId, lb.LeaveTypeId, lb.Year })
                .IsUnique();

            // LeaveApplication relationships
            modelBuilder.Entity<LeaveApplication>()
                .HasOne(la => la.Employee)
                .WithMany()
                .HasForeignKey(la => la.EmployeeId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<LeaveApplication>()
                .HasOne(la => la.LeaveType)
                .WithMany(lt => lt.LeaveApplications)
                .HasForeignKey(la => la.LeaveTypeId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<LeaveApplication>()
                .HasOne(la => la.ReviewedBy)
                .WithMany()
                .HasForeignKey(la => la.ReviewedById)
                .OnDelete(DeleteBehavior.Restrict);

            // Indexes for performance
            modelBuilder.Entity<LeaveApplication>()
                .HasIndex(la => la.Status);

            modelBuilder.Entity<LeaveApplication>()
                .HasIndex(la => la.StartDate);

            modelBuilder.Entity<LeaveApplication>()
                .HasIndex(la => la.AppliedDate);

            modelBuilder.Entity<LeaveType>()
                .HasIndex(lt => lt.Name)
                .IsUnique();
        }
    }
}