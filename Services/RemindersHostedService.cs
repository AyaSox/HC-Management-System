using HRManagementSystem.Data;
using Microsoft.EntityFrameworkCore;

namespace HRManagementSystem.Services;

public class RemindersHostedService : BackgroundService
{
    private readonly IServiceProvider _provider;
    private readonly ILogger<RemindersHostedService> _logger;

    public RemindersHostedService(IServiceProvider provider, ILogger<RemindersHostedService> logger)
    {
        _provider = provider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Run daily at 06:00 server time
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var now = DateTime.Now;
                var nextRun = new DateTime(now.Year, now.Month, now.Day, 6, 0, 0);
                if (nextRun <= now) nextRun = nextRun.AddDays(1);
                var delay = nextRun - now;
                await Task.Delay(delay, stoppingToken);

                using var scope = _provider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                var employees = await db.Employees
                    .AsNoTracking()
                    .Where(e => e.DateOfBirth.HasValue || e.DateHired != default)
                    .ToListAsync(stoppingToken);

                var upcomingBirthdays = employees.Where(e => e.HasUpcomingBirthday).ToList();
                var upcomingAnniversaries = employees.Where(e => e.HasUpcomingAnniversary).ToList();

                _logger.LogInformation("Reminder tick: {Birthdays} birthdays, {Anniversaries} anniversaries upcoming.",
                    upcomingBirthdays.Count, upcomingAnniversaries.Count);

                // TODO: plug in email/Teams/Slack notifications if required.
            }
            catch (TaskCanceledException)
            {
                // ignore on shutdown
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in RemindersHostedService");
            }
        }
    }
}
