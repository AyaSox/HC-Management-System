using HRManagement.Shared.Models;
using HRManagement.Shared.DTOs;
using System.Text.Json;

namespace HRManagementSystem.Services
{
    public class ESSLeaveApiClient : IESSLeaveApiClient
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<ESSLeaveApiClient> _logger;
        private readonly IConfiguration _configuration;
        private readonly JsonSerializerOptions _jsonOptions;

        public ESSLeaveApiClient(HttpClient httpClient, ILogger<ESSLeaveApiClient> logger, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _logger = logger;
            _configuration = configuration;

            // Configure base URL from environment variable or config
            var baseUrl = _configuration["ESS_LEAVE_API_URL"] ?? "http://localhost:5100";
            _httpClient.BaseAddress = new Uri(baseUrl);
            
            _jsonOptions = new JsonSerializerOptions 
            { 
                PropertyNameCaseInsensitive = true 
            };
        }

        public async Task<ApiResponse<List<LeaveBalance>>> GetEmployeeLeaveBalancesAsync(int employeeId, int year)
        {
            try
            {
                var response = await _httpClient.GetAsync($"api/LeaveBalances/employee/{employeeId}/year/{year}");
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var result = JsonSerializer.Deserialize<ApiResponse<List<LeaveBalance>>>(content, _jsonOptions);
                    return result ?? new ApiResponse<List<LeaveBalance>> { Success = false, Message = "Failed to deserialize response" };
                }
                
                _logger.LogWarning($"ESS Leave API returned {response.StatusCode} for GetEmployeeLeaveBalances");
                return new ApiResponse<List<LeaveBalance>> 
                { 
                    Success = false, 
                    Message = $"API returned status code: {response.StatusCode}",
                    Data = new List<LeaveBalance>()
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling ESS Leave API - GetEmployeeLeaveBalances");
                return new ApiResponse<List<LeaveBalance>> 
                { 
                    Success = false, 
                    Message = "Unable to connect to Leave API. Feature temporarily unavailable.",
                    Data = new List<LeaveBalance>()
                };
            }
        }

        public async Task<ApiResponse<List<LeaveApplication>>> GetEmployeeLeaveApplicationsAsync(int employeeId)
        {
            try
            {
                var response = await _httpClient.GetAsync($"api/LeaveApplications/employee/{employeeId}");
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var result = JsonSerializer.Deserialize<ApiResponse<List<LeaveApplication>>>(content, _jsonOptions);
                    return result ?? new ApiResponse<List<LeaveApplication>> { Success = false, Message = "Failed to deserialize response" };
                }
                
                _logger.LogWarning($"ESS Leave API returned {response.StatusCode} for GetEmployeeLeaveApplications");
                return new ApiResponse<List<LeaveApplication>> 
                { 
                    Success = false, 
                    Message = $"API returned status code: {response.StatusCode}",
                    Data = new List<LeaveApplication>()
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling ESS Leave API - GetEmployeeLeaveApplications");
                return new ApiResponse<List<LeaveApplication>> 
                { 
                    Success = false, 
                    Message = "Unable to connect to Leave API. Feature temporarily unavailable.",
                    Data = new List<LeaveApplication>()
                };
            }
        }

        public async Task<ApiResponse<List<LeaveApplication>>> GetPendingLeaveApplicationsForManagerAsync(int managerId)
        {
            try
            {
                var response = await _httpClient.GetAsync($"api/LeaveApplications/manager/{managerId}/pending");
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var result = JsonSerializer.Deserialize<ApiResponse<List<LeaveApplication>>>(content, _jsonOptions);
                    return result ?? new ApiResponse<List<LeaveApplication>> { Success = false, Message = "Failed to deserialize response" };
                }
                
                _logger.LogWarning($"ESS Leave API returned {response.StatusCode} for GetPendingLeaveApplicationsForManager");
                return new ApiResponse<List<LeaveApplication>> 
                { 
                    Success = false, 
                    Message = $"API returned status code: {response.StatusCode}",
                    Data = new List<LeaveApplication>()
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling ESS Leave API - GetPendingLeaveApplicationsForManager");
                return new ApiResponse<List<LeaveApplication>> 
                { 
                    Success = false, 
                    Message = "Unable to connect to Leave API. Feature temporarily unavailable.",
                    Data = new List<LeaveApplication>()
                };
            }
        }

        public async Task<ApiResponse<List<LeaveType>>> GetLeaveTypesAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync("api/LeaveTypes");
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var result = JsonSerializer.Deserialize<ApiResponse<List<LeaveType>>>(content, _jsonOptions);
                    return result ?? new ApiResponse<List<LeaveType>> { Success = false, Message = "Failed to deserialize response" };
                }
                
                _logger.LogWarning($"ESS Leave API returned {response.StatusCode} for GetLeaveTypes");
                return new ApiResponse<List<LeaveType>> 
                { 
                    Success = false, 
                    Message = $"API returned status code: {response.StatusCode}",
                    Data = new List<LeaveType>()
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling ESS Leave API - GetLeaveTypes");
                return new ApiResponse<List<LeaveType>> 
                { 
                    Success = false, 
                    Message = "Unable to connect to Leave API. Feature temporarily unavailable.",
                    Data = new List<LeaveType>()
                };
            }
        }

        public async Task<ApiResponse<LeaveApplication>> CreateLeaveApplicationAsync(LeaveApplicationRequest request)
        {
            try
            {
                var jsonContent = JsonSerializer.Serialize(request, _jsonOptions);
                var content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");
                
                var response = await _httpClient.PostAsync("api/LeaveApplications", content);
                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var result = JsonSerializer.Deserialize<ApiResponse<LeaveApplication>>(responseContent, _jsonOptions);
                    return result ?? new ApiResponse<LeaveApplication> { Success = false, Message = "Failed to deserialize response" };
                }
                
                _logger.LogWarning($"ESS Leave API returned {response.StatusCode} for CreateLeaveApplication");
                return new ApiResponse<LeaveApplication> 
                { 
                    Success = false, 
                    Message = $"Failed to submit leave application. API returned: {response.StatusCode}"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling ESS Leave API - CreateLeaveApplication");
                return new ApiResponse<LeaveApplication> 
                { 
                    Success = false, 
                    Message = "Unable to connect to Leave API. Please try again later."
                };
            }
        }

        public async Task<ApiResponse<LeaveApplication>> ApproveLeaveApplicationAsync(int applicationId, LeaveApprovalRequest request)
        {
            try
            {
                var jsonContent = JsonSerializer.Serialize(request, _jsonOptions);
                var content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");
                
                var response = await _httpClient.PostAsync($"api/LeaveApplications/{applicationId}/approve", content);
                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var result = JsonSerializer.Deserialize<ApiResponse<LeaveApplication>>(responseContent, _jsonOptions);
                    return result ?? new ApiResponse<LeaveApplication> { Success = false, Message = "Failed to deserialize response" };
                }
                
                _logger.LogWarning($"ESS Leave API returned {response.StatusCode} for ApproveLeaveApplication");
                return new ApiResponse<LeaveApplication> 
                { 
                    Success = false, 
                    Message = $"Failed to approve leave application. API returned: {response.StatusCode}"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling ESS Leave API - ApproveLeaveApplication");
                return new ApiResponse<LeaveApplication> 
                { 
                    Success = false, 
                    Message = "Unable to connect to Leave API. Please try again later."
                };
            }
        }

        public async Task<ApiResponse<List<LeaveBalance>>> InitializeLeaveBalancesAsync(int employeeId, int year)
        {
            try
            {
                var request = new { EmployeeId = employeeId, Year = year };
                var jsonContent = JsonSerializer.Serialize(request, _jsonOptions);
                var content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");
                
                var response = await _httpClient.PostAsync("api/LeaveBalances/initialize", content);
                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var result = JsonSerializer.Deserialize<ApiResponse<List<LeaveBalance>>>(responseContent, _jsonOptions);
                    return result ?? new ApiResponse<List<LeaveBalance>> { Success = false, Message = "Failed to deserialize response" };
                }
                
                _logger.LogWarning($"ESS Leave API returned {response.StatusCode} for InitializeLeaveBalances");
                return new ApiResponse<List<LeaveBalance>> 
                { 
                    Success = false, 
                    Message = $"Failed to initialize leave balances. API returned: {response.StatusCode}",
                    Data = new List<LeaveBalance>()
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling ESS Leave API - InitializeLeaveBalances");
                return new ApiResponse<List<LeaveBalance>> 
                { 
                    Success = false, 
                    Message = "Unable to connect to Leave API.",
                    Data = new List<LeaveBalance>()
                };
            }
        }
    }
}
