using System.Diagnostics;

namespace HRManagementSystem.Middleware
{
    public class StartupTimingMiddleware
    {
        private readonly RequestDelegate _next;
        private static readonly DateTime _startTime = DateTime.UtcNow;

        public StartupTimingMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // Add startup time to response headers for first request
            if (!context.Response.Headers.ContainsKey("X-Startup-Time"))
            {
                var startupTime = DateTime.UtcNow - _startTime;
                context.Response.Headers.Add("X-Startup-Time", startupTime.TotalSeconds.ToString("F2"));
            }

            await _next(context);
        }
    }
}