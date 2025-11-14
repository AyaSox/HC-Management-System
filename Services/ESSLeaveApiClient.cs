using HRManagement.Shared.Models;
using HRManagement.Shared.DTOs;
using System.Text.Json;

namespace HRManagementSystem.Services
{
    public interface IESSLeaveApiClient
    {
        Task<ApiResponse<List<LeaveType>>> GetLeaveTypesAsync();
        Task<ApiResponse<List<LeaveBalance>>> GetEmployeeLeaveBalancesAsync(int employeeId, int year);
        Task<ApiResponse<List<LeaveApplication>>> GetEmployeeLeaveApplicationsAsync(int employeeId);
        Task<ApiResponse<List<LeaveApplication>>> GetPendingLeaveApplicationsForManagerAsync(int managerId);
        Task<ApiResponse<LeaveApplication>> CreateLeaveApplicationAsync(LeaveApplicationRequest request);
        Task<ApiResponse<LeaveApplication>> ApproveLeaveApplicationAsync(int applicationId, LeaveApprovalRequest request);
        Task<ApiResponse<List<LeaveBalance>>> InitializeLeaveBalancesAsync(int employeeId, int year);
    }

    public class ESSLeaveApiClient : IESSLeaveApiClient
    {
        private readonly HttpClient _httpClient;
        private readonly JsonSerializerOptions _jsonOptions;

        public ESSLeaveApiClient(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            
            // Configure base address (could be from configuration)
            var essApiUrl = configuration.GetValue<string>("ESSApi:BaseUrl") ?? "http://localhost:5100";
            _httpClient.BaseAddress = new Uri(essApiUrl);
            
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true
            };
        }

        public async Task<ApiResponse<List<LeaveType>>> GetLeaveTypesAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync("api/LeaveTypes");
                var jsonContent = await response.Content.ReadAsStringAsync();
                
                if (response.IsSuccessStatusCode)
                {
                    return JsonSerializer.Deserialize<ApiResponse<List<LeaveType>>>(jsonContent, _jsonOptions) 
                        ?? new ApiResponse<List<LeaveType>> { Success = false, Message = "Failed to deserialize response" };
                }
                
                return new ApiResponse<List<LeaveType>>
                {
                    Success = false,
                    Message = $"API call failed: {response.StatusCode}"
                };
            }
            catch (Exception ex)
            {
                return new ApiResponse<List<LeaveType>>
                {
                    Success = false,
                    Message = "Failed to connect to ESS Leave API",
                    Errors = new List<string> { ex.Message }
                };
            }
        }

        public async Task<ApiResponse<List<LeaveBalance>>> GetEmployeeLeaveBalancesAsync(int employeeId, int year)
        {
            try
            {
                var response = await _httpClient.GetAsync($"api/LeaveBalances/employee/{employeeId}/year/{year}");
                var jsonContent = await response.Content.ReadAsStringAsync();
                
                if (response.IsSuccessStatusCode)
                {
                    return JsonSerializer.Deserialize<ApiResponse<List<LeaveBalance>>>(jsonContent, _jsonOptions) 
                        ?? new ApiResponse<List<LeaveBalance>> { Success = false, Message = "Failed to deserialize response" };
                }
                
                return new ApiResponse<List<LeaveBalance>>
                {
                    Success = false,
                    Message = $"API call failed: {response.StatusCode}"
                };
            }
            catch (Exception ex)
            {
                return new ApiResponse<List<LeaveBalance>>
                {
                    Success = false,
                    Message = "Failed to connect to ESS Leave API",
                    Errors = new List<string> { ex.Message }
                };
            }
        }

        public async Task<ApiResponse<List<LeaveApplication>>> GetEmployeeLeaveApplicationsAsync(int employeeId)
        {
            try
            {
                var response = await _httpClient.GetAsync($"api/LeaveApplications/employee/{employeeId}");
                var jsonContent = await response.Content.ReadAsStringAsync();
                
                if (response.IsSuccessStatusCode)
                {
                    return JsonSerializer.Deserialize<ApiResponse<List<LeaveApplication>>>(jsonContent, _jsonOptions) 
                        ?? new ApiResponse<List<LeaveApplication>> { Success = false, Message = "Failed to deserialize response" };
                }
                
                return new ApiResponse<List<LeaveApplication>>
                {
                    Success = false,
                    Message = $"API call failed: {response.StatusCode}"
                };
            }
            catch (Exception ex)
            {
                return new ApiResponse<List<LeaveApplication>>
                {
                    Success = false,
                    Message = "Failed to connect to ESS Leave API",
                    Errors = new List<string> { ex.Message }
                };
            }
        }

        public async Task<ApiResponse<List<LeaveApplication>>> GetPendingLeaveApplicationsForManagerAsync(int managerId)
        {
            try
            {
                var response = await _httpClient.GetAsync($"api/LeaveApplications/manager/{managerId}/pending");
                var jsonContent = await response.Content.ReadAsStringAsync();
                
                if (response.IsSuccessStatusCode)
                {
                    return JsonSerializer.Deserialize<ApiResponse<List<LeaveApplication>>>(jsonContent, _jsonOptions) 
                        ?? new ApiResponse<List<LeaveApplication>> { Success = false, Message = "Failed to deserialize response" };
                }
                
                return new ApiResponse<List<LeaveApplication>>
                {
                    Success = false,
                    Message = $"API call failed: {response.StatusCode}"
                };
            }
            catch (Exception ex)
            {
                return new ApiResponse<List<LeaveApplication>>
                {
                    Success = false,
                    Message = "Failed to connect to ESS Leave API",
                    Errors = new List<string> { ex.Message }
                };
            }
        }

        public async Task<ApiResponse<LeaveApplication>> CreateLeaveApplicationAsync(LeaveApplicationRequest request)
        {
            return await CreateLeaveApplicationWithFileAsync(request, null);
        }

        public async Task<ApiResponse<LeaveApplication>> CreateLeaveApplicationWithFileAsync(LeaveApplicationRequest request, IFormFile? supportingDocument)
        {
            try
            {
                MultipartFormDataContent content;
                
                if (supportingDocument != null)
                {
                    // Use multipart form data for file upload
                    content = new MultipartFormDataContent();
                    content.Add(new StringContent(request.EmployeeId.ToString()), "EmployeeId");
                    content.Add(new StringContent(request.LeaveTypeId.ToString()), "LeaveTypeId");
                    content.Add(new StringContent(request.StartDate.ToString("yyyy-MM-dd")), "StartDate");
                    content.Add(new StringContent(request.EndDate.ToString("yyyy-MM-dd")), "EndDate");
                    content.Add(new StringContent(request.Reason), "Reason");
                    
                    if (!string.IsNullOrEmpty(request.ContactDuringLeave))
                        content.Add(new StringContent(request.ContactDuringLeave), "ContactDuringLeave");
                    
                    var fileContent = new StreamContent(supportingDocument.OpenReadStream());
                    fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(supportingDocument.ContentType);
                    content.Add(fileContent, "SupportingDocument", supportingDocument.FileName);
                }
                else
                {
                    // Use JSON for requests without files
                    var jsonContent = JsonSerializer.Serialize(new
                    {
                        request.EmployeeId,
                        request.LeaveTypeId,
                        request.StartDate,
                        request.EndDate,
                        request.Reason,
                        request.ContactDuringLeave
                    }, _jsonOptions);
                    content = new MultipartFormDataContent();
                    content.Add(new StringContent(request.EmployeeId.ToString()), "EmployeeId");
                    content.Add(new StringContent(request.LeaveTypeId.ToString()), "LeaveTypeId");
                    content.Add(new StringContent(request.StartDate.ToString("yyyy-MM-dd")), "StartDate");
                    content.Add(new StringContent(request.EndDate.ToString("yyyy-MM-dd")), "EndDate");
                    content.Add(new StringContent(request.Reason), "Reason");
                    
                    if (!string.IsNullOrEmpty(request.ContactDuringLeave))
                        content.Add(new StringContent(request.ContactDuringLeave), "ContactDuringLeave");
                }
                
                var response = await _httpClient.PostAsync("api/LeaveApplications", content);
                var responseContent = await response.Content.ReadAsStringAsync();
                
                if (response.IsSuccessStatusCode)
                {
                    return JsonSerializer.Deserialize<ApiResponse<LeaveApplication>>(responseContent, _jsonOptions) 
                        ?? new ApiResponse<LeaveApplication> { Success = false, Message = "Failed to deserialize response" };
                }
                
                return new ApiResponse<LeaveApplication>
                {
                    Success = false,
                    Message = $"API call failed: {response.StatusCode}",
                    Errors = new List<string> { responseContent }
                };
            }
            catch (Exception ex)
            {
                return new ApiResponse<LeaveApplication>
                {
                    Success = false,
                    Message = "Failed to connect to ESS Leave API",
                    Errors = new List<string> { ex.Message }
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
                var responseContent = await response.Content.ReadAsStringAsync();
                
                if (response.IsSuccessStatusCode)
                {
                    return JsonSerializer.Deserialize<ApiResponse<LeaveApplication>>(responseContent, _jsonOptions) 
                        ?? new ApiResponse<LeaveApplication> { Success = false, Message = "Failed to deserialize response" };
                }
                
                return new ApiResponse<LeaveApplication>
                {
                    Success = false,
                    Message = $"API call failed: {response.StatusCode}",
                    Errors = new List<string> { responseContent }
                };
            }
            catch (Exception ex)
            {
                return new ApiResponse<LeaveApplication>
                {
                    Success = false,
                    Message = "Failed to connect to ESS Leave API",
                    Errors = new List<string> { ex.Message }
                };
            }
        }

        public async Task<ApiResponse<List<LeaveBalance>>> InitializeLeaveBalancesAsync(int employeeId, int year)
        {
            try
            {
                var request = new LeaveBalanceInitializationRequest
                {
                    EmployeeId = employeeId,
                    Year = year
                };

                var jsonContent = JsonSerializer.Serialize(request, _jsonOptions);
                var content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");
                
                var response = await _httpClient.PostAsync("api/LeaveBalances/initialize", content);
                var responseContent = await response.Content.ReadAsStringAsync();
                
                if (response.IsSuccessStatusCode)
                {
                    return JsonSerializer.Deserialize<ApiResponse<List<LeaveBalance>>>(responseContent, _jsonOptions) 
                        ?? new ApiResponse<List<LeaveBalance>> { Success = false, Message = "Failed to deserialize response" };
                }
                
                return new ApiResponse<List<LeaveBalance>>
                {
                    Success = false,
                    Message = $"API call failed: {response.StatusCode}",
                    Errors = new List<string> { responseContent }
                };
            }
            catch (Exception ex)
            {
                return new ApiResponse<List<LeaveBalance>>
                {
                    Success = false,
                    Message = "Failed to connect to ESS Leave API",
                    Errors = new List<string> { ex.Message }
                };
            }
        }
    }
}