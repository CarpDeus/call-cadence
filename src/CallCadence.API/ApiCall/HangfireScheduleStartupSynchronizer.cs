using Hangfire;
using Hangfire.Storage;
using Microsoft.EntityFrameworkCore;

namespace CallCadence.Infrastructure.ApiCall;

/// <summary>
/// Reconciles persisted API call schedules with Hangfire recurring jobs at startup.
/// </summary>
public sealed class HangfireScheduleStartupSynchronizer
{
    private const string JobIdPrefix = "schedule-";

    private readonly CallCadenceDbContext _dbContext;
    private readonly IRecurringJobManager _recurringJobManager;
    private readonly ILogger<HangfireScheduleStartupSynchronizer> _logger;

    public HangfireScheduleStartupSynchronizer(
        CallCadenceDbContext dbContext,
        IRecurringJobManager recurringJobManager,
        ILogger<HangfireScheduleStartupSynchronizer> logger)
    {
        _dbContext = dbContext;
        _recurringJobManager = recurringJobManager;
        _logger = logger;
    }

    public async Task SynchronizeAsync()
    {
        using var connection = JobStorage.Current?.GetConnection();
        if (connection is null)
        {
            _logger.LogWarning("Skipping Hangfire startup synchronization because no Hangfire storage connection is available.");
            return;
        }

        var schedules = await _dbContext.ApiCallSchedules
            .AsNoTracking()
            .Where(schedule => schedule.IsEnabled)
            .ToListAsync();
        var activeApiCalls = await _dbContext.ApiCalls
            .AsNoTracking()
            .Where(call => call.IsActive)
            .Select(call => call.Id)
            .ToHashSetAsync();
        var recurringJobs = connection.GetRecurringJobs()
            .ToDictionary(job => job.Id, StringComparer.OrdinalIgnoreCase);

        var activeSchedules = schedules
            .Where(schedule => activeApiCalls.Contains(schedule.ApiCallId))
            .ToDictionary(schedule => schedule.Id);

        var removedJobs = 0;
        foreach (var recurringJob in recurringJobs.Values)
        {
            if (!TryGetScheduleId(recurringJob.Id, out var scheduleId))
            {
                continue;
            }

            if (!activeSchedules.TryGetValue(scheduleId, out var schedule))
            {
                _recurringJobManager.RemoveIfExists(recurringJob.Id);
                removedJobs++;
                continue;
            }

            if (SchedulesMatch(schedule.CronExpression, recurringJob.Cron))
            {
                continue;
            }

            _recurringJobManager.RemoveIfExists(recurringJob.Id);
            removedJobs++;
        }

        var addedJobs = 0;
        foreach (var schedule in activeSchedules.Values)
        {
            var jobId = GetJobId(schedule.Id);
            if (recurringJobs.TryGetValue(jobId, out var recurringJob)
                && SchedulesMatch(schedule.CronExpression, recurringJob.Cron)
                && HasCurrentSignature(recurringJob))
            {
                continue;
            }

            _recurringJobManager.AddOrUpdate<CallApiService>(
                jobId,
                service => service.ExecuteApiCallAsync(schedule.ApiCallId, schedule.Id),
                schedule.CronExpression);
            addedJobs++;
        }

        _logger.LogInformation(
            "Hangfire startup synchronization complete. Added jobs: {AddedJobs}; removed jobs: {RemovedJobs}.",
            addedJobs,
            removedJobs);
    }

    private static string GetJobId(Guid scheduleId)
    {
        return $"{JobIdPrefix}{scheduleId}";
    }

    private static bool TryGetScheduleId(string jobId, out Guid scheduleId)
    {
        scheduleId = Guid.Empty;

        if (!jobId.StartsWith(JobIdPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return Guid.TryParse(jobId[JobIdPrefix.Length..], out scheduleId);
    }

    private static bool SchedulesMatch(string dbCronExpression, string? hangfireCronExpression)
    {
        return string.Equals(
            dbCronExpression.Trim(),
            (hangfireCronExpression ?? string.Empty).Trim(),
            StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasCurrentSignature(Hangfire.Storage.RecurringJobDto recurringJob)
    {
        // ExecuteApiCallAsync now takes two arguments (apiCallId, scheduleId).
        // Jobs registered with the old single-argument signature must be re-registered.
        return recurringJob.Job?.Args?.Count == 2;
    }
}
