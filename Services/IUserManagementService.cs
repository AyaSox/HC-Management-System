using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace HRManagementSystem.Services
{
    public interface IUserManagementService
    {
        /// <summary>
        /// Create Identity user for a new employee
        /// </summary>
        Task<(bool Success, string Password, string ErrorMessage)> CreateIdentityUserForEmployeeAsync(string email, string fullName);
        
        /// <summary>
        /// Auto-assign roles based on organizational structure
        /// </summary>
        Task AssignRolesBasedOnOrgStructureAsync(string email);
        
        /// <summary>
        /// Update user roles when employee details change
        /// </summary>
        Task UpdateEmployeeRolesAsync(string email);
        
        /// <summary>
        /// Generate secure temporary password
        /// </summary>
        string GenerateTemporaryPassword();
        
        /// <summary>
        /// Check if user exists
        /// </summary>
        Task<bool> UserExistsAsync(string email);
    }

    public class UserManagementService : IUserManagementService
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ILogger<UserManagementService> _logger;

        public UserManagementService(
            UserManager<IdentityUser> userManager,
            RoleManager<IdentityRole> roleManager,
            ILogger<UserManagementService> logger)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _logger = logger;
        }

        public async Task<(bool Success, string Password, string ErrorMessage)> CreateIdentityUserForEmployeeAsync(string email, string fullName)
        {
            try
            {
                // Check if user already exists
                var existingUser = await _userManager.FindByEmailAsync(email);
                if (existingUser != null)
                {
                    _logger.LogWarning($"User already exists: {email}");
                    return (false, string.Empty, "User already exists");
                }

                // Generate temporary password
                var tempPassword = GenerateTemporaryPassword();

                // Create new Identity user
                var user = new IdentityUser
                {
                    UserName = email,
                    Email = email,
                    EmailConfirmed = true // Auto-confirm for internal system
                };

                var result = await _userManager.CreateAsync(user, tempPassword);

                if (!result.Succeeded)
                {
                    var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                    _logger.LogError($"Failed to create user {email}: {errors}");
                    return (false, string.Empty, errors);
                }

                // Assign base Employee role
                await EnsureRoleExistsAsync("Employee");
                await _userManager.AddToRoleAsync(user, "Employee");

                // Auto-assign roles based on organizational structure
                await AssignRolesBasedOnOrgStructureAsync(email);

                _logger.LogInformation($"? Created Identity user for {fullName} ({email})");
                _logger.LogInformation($"? Temporary password: {tempPassword}");

                return (true, tempPassword, string.Empty);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error creating Identity user for {email}");
                return (false, string.Empty, ex.Message);
            }
        }

        public async Task AssignRolesBasedOnOrgStructureAsync(string email)
        {
            // This will be implemented to check:
            // - If employee has direct reports ? Manager role
            // - If job title contains "Senior", "Head", "Director" ? SeniorManager role
            // - If department is "Human Capital" ? HR role
            
            // For now, just assign Employee role
            var user = await _userManager.FindByEmailAsync(email);
            if (user == null) return;

            await EnsureRoleExistsAsync("Employee");
            
            if (!await _userManager.IsInRoleAsync(user, "Employee"))
            {
                await _userManager.AddToRoleAsync(user, "Employee");
            }

            _logger.LogInformation($"? Assigned Employee role to {email}");
        }

        public async Task UpdateEmployeeRolesAsync(string email)
        {
            var user = await _userManager.FindByEmailAsync(email);
            if (user == null) return;

            // Remove all roles except Admin
            var currentRoles = await _userManager.GetRolesAsync(user);
            var rolesToRemove = currentRoles.Where(r => r != "Admin").ToList();

            if (rolesToRemove.Any())
            {
                await _userManager.RemoveFromRolesAsync(user, rolesToRemove);
            }

            // Re-assign based on current org structure
            await AssignRolesBasedOnOrgStructureAsync(email);
        }

        public string GenerateTemporaryPassword()
        {
            // Generate secure temporary password
            // Format: Welcome@XXXX where XXXX is random (guaranteed to have at least one digit)
            const string letters = "ABCDEFGHJKLMNPQRSTUVWXYZ"; // Exclude ambiguous characters
            const string digits = "23456789"; // Exclude 0 and 1 (ambiguous)
            
            var random = new Random();
            
            // Generate 3 letters and 1 digit to ensure password requirements are met
            var letter1 = letters[random.Next(letters.Length)];
            var letter2 = letters[random.Next(letters.Length)];
            var letter3 = letters[random.Next(letters.Length)];
            var digit = digits[random.Next(digits.Length)];
            
            // Shuffle them randomly
            var parts = new[] { letter1.ToString(), letter2.ToString(), letter3.ToString(), digit.ToString() };
            var shuffled = parts.OrderBy(_ => random.Next()).ToArray();
            var randomPart = string.Join("", shuffled);

            return $"Welcome@{randomPart}";
        }

        public async Task<bool> UserExistsAsync(string email)
        {
            var user = await _userManager.FindByEmailAsync(email);
            return user != null;
        }

        private async Task EnsureRoleExistsAsync(string roleName)
        {
            if (!await _roleManager.RoleExistsAsync(roleName))
            {
                await _roleManager.CreateAsync(new IdentityRole(roleName));
                _logger.LogInformation($"? Created role: {roleName}");
            }
        }
    }
}