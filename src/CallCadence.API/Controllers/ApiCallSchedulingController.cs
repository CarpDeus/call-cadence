using CallCadence.Application.ApiCall;
using CallCadence.Domain.ApiCall;
using CallCadence.Domain.Paging;
using CallCadence.Infrastructure.ApiCall;
using Cronos;
using Hangfire;
using Hangfire.Storage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CallCadence.API.Controllers;

/// <summary>
/// Controller for scheduling API calls using Hangfire.
/// </summary>
[ApiController]
[Authorize]
[Route("api/[controller]")]
public sealed class ApiCallSchedulingController : ControllerBase
{
    private const string JobIdPrefix = "schedule-";

    private readonly CallApiService _callApiService;
    private readonly IApiCallRepository _apiCallRepository;
    private readonly CallCadenceDbContext _dbContext;
    private readonly IRecurringJobManager _recurringJobManager;

    public ApiCallSchedulingController(
        CallApiService callApiService,
        IApiCallRepository apiCallRepository,
        CallCadenceDbContext dbContext,
        IRecurringJobManager recurringJobManager)
    {
        _callApiService = callApiService;
        _apiCallRepository = apiCallRepository;
        _dbContext = dbContext;
        _recurringJobManager = recurringJobManager;
    }

    /// <summary>
    /// Schedule an API call with a cron expression.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<ScheduleResponse>> ScheduleApiCall([FromBody] ScheduleRequest request)
    {
        try
        {
            var response = await UpsertScheduleAsync(request);
            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ex.Message);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Schedule multiple API calls with their cron expressions.
    /// </summary>
    [HttpPost("bulk")]
    public async Task<ActionResult<IEnumerable<ScheduleResponse>>> ScheduleApiCalls([FromBody] IEnumerable<ScheduleRequest> requests)
    {
        try
        {
            var responses = new List<ScheduleResponse>();

            foreach (var request in requests)
            {
                responses.Add(await UpsertScheduleAsync(request));
            }

            return Ok(responses);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ex.Message);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Remove a scheduled API call.
    /// </summary>
    [HttpDelete("{scheduleId}")]
    public async Task<ActionResult> RemoveSchedule(string scheduleId)
    {
        if (!TryGetScheduleId(scheduleId, out var parsedScheduleId))
        {
            return BadRequest($"Invalid schedule ID or job ID '{scheduleId}'.");
        }

        var schedule = await _dbContext.ApiCallSchedules.FirstOrDefaultAsync(x => x.Id == parsedScheduleId);
        if (schedule != null)
        {
            _dbContext.ApiCallSchedules.Remove(schedule);
            await _dbContext.SaveChangesAsync();
        }

        _recurringJobManager.RemoveIfExists(GetJobId(parsedScheduleId));
        return Ok(new { Message = "Schedule removed successfully" });
    }

    /// <summary>
    /// Remove multiple scheduled API calls.
    /// </summary>
    [HttpDelete("bulk")]
    public async Task<ActionResult> RemoveSchedules([FromBody] IEnumerable<string> scheduleIds)
    {
        var removedCount = 0;

        foreach (var scheduleId in scheduleIds)
        {
            if (!TryGetScheduleId(scheduleId, out var parsedScheduleId))
            {
                continue;
            }

            var schedule = await _dbContext.ApiCallSchedules.FirstOrDefaultAsync(x => x.Id == parsedScheduleId);
            if (schedule != null)
            {
                _dbContext.ApiCallSchedules.Remove(schedule);
                removedCount++;
            }

            _recurringJobManager.RemoveIfExists(GetJobId(parsedScheduleId));
        }

        if (removedCount > 0)
        {
            await _dbContext.SaveChangesAsync();
        }

        return Ok(new { Message = $"{removedCount} schedule(s) removed successfully" });
    }

    /// <summary>
    /// Get all execution logs.
    /// </summary>
    [HttpGet("logs")]
    public async Task<ActionResult<IEnumerable<ApiCallLogDto>>> GetAllLogs()
    {
        var logs = await _dbContext.ApiCallLogs.AsNoTracking().ToListAsync();
        return Ok(logs.Select(MapToDto));
    }

    /// <summary>
    /// Get execution logs for a specific API call.
    /// </summary>
    [HttpGet("logs/{apiCallId}")]
    public async Task<ActionResult<IEnumerable<ApiCallLogDto>>> GetLogsByApiCallId(Guid apiCallId)
    {
        var logs = await _dbContext.ApiCallLogs
            .AsNoTracking()
            .Where(log => log.ApiCallId == apiCallId)
            .ToListAsync();
        return Ok(logs.Select(MapToDto));
    }

    /// <summary>
    /// Get a paged, sortable list of execution logs for a specific API call.
    /// </summary>
    [HttpGet("logs/{apiCallId}/list")]
    public async Task<ActionResult<PagedResult<ApiCallLogDto>>> GetLogsByApiCallIdPaged(
        Guid apiCallId, [FromQuery] ApiCallLogListRequest request)
    {
        var pageNumber = request.PageNumber < 1 ? 1 : request.PageNumber;
        var pageSize = request.PageSize <= 0 ? 10 : Math.Min(request.PageSize, 100);
        var sortBy = string.IsNullOrWhiteSpace(request.SortBy) ? "executedAt" : request.SortBy.Trim().ToLowerInvariant();

        var baseQuery = _dbContext.ApiCallLogs
            .AsNoTracking()
            .Where(log => log.ApiCallId == apiCallId);

        // sortBy is already lowercased above; sort labels from the client (e.g. "responseCode") match after lowercasing
        IQueryable<ApiCallLog> orderedQuery = (sortBy, request.SortDescending) switch
        {
            ("responsecode", false) => baseQuery.OrderBy(x => x.ResponseCode),
            ("responsecode", true) => baseQuery.OrderByDescending(x => x.ResponseCode),
            ("durationms", false) => baseQuery.OrderBy(x => x.DurationMs),
            ("durationms", true) => baseQuery.OrderByDescending(x => x.DurationMs),
            ("success", false) => baseQuery.OrderBy(x => x.Success),
            ("success", true) => baseQuery.OrderByDescending(x => x.Success),
            (_, false) => baseQuery.OrderBy(x => x.ExecutedAt),
            (_, true) => baseQuery.OrderByDescending(x => x.ExecutedAt),
        };

        var totalItems = await orderedQuery.CountAsync();
        var pageCount = totalItems == 0 ? 0 : (int)Math.Ceiling(totalItems / (double)pageSize);
        var items = await orderedQuery
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return Ok(new PagedResult<ApiCallLogDto>(
            new Paging(pageCount, totalItems, pageNumber, pageSize),
            items.Select(MapToDto)));
    }

    /// <summary>
    /// Get all scheduled API calls with their details.
    /// </summary>
    [HttpGet("schedules")]
    public async Task<ActionResult<IEnumerable<ScheduleInfoResponse>>> GetSchedules()
    {
        var schedules = await _dbContext.ApiCallSchedules.AsNoTracking().ToListAsync();
        var scheduleMap = GetRecurringJobMap();
        var responses = new List<ScheduleInfoResponse>();

        foreach (var schedule in schedules)
        {
            var apiCall = await _apiCallRepository.GetByIdAsync(schedule.ApiCallId);
            if (apiCall == null)
            {
                continue;
            }

            scheduleMap.TryGetValue(GetJobId(schedule.Id), out var recurringJob);

            responses.Add(new ScheduleInfoResponse
            {
                ScheduleId = schedule.Id,
                JobId = GetJobId(schedule.Id),
                ApiCallId = apiCall.Id,
                Title = apiCall.Title,
                Description = apiCall.Description,
                HttpMethod = apiCall.HttpMethod,
                IsActive = apiCall.IsActive,
                IsEnabled = schedule.IsEnabled,
                NextExecution = schedule.IsEnabled ? recurringJob?.NextExecution : null,
                CronExpression = schedule.CronExpression
            });
        }

        return Ok(responses);
    }

    /// <summary>
    /// Remove schedules for inactive API calls.
    /// </summary>
    [HttpDelete("inactive")]
    public async Task<ActionResult> RemoveInactive()
    {
        var schedules = await _dbContext.ApiCallSchedules.AsNoTracking().ToListAsync();
        var removedCount = 0;

        foreach (var schedule in schedules)
        {
            var apiCall = await _apiCallRepository.GetByIdAsync(schedule.ApiCallId);
            if (apiCall != null && !apiCall.IsActive)
            {
                var existing = await _dbContext.ApiCallSchedules.FirstOrDefaultAsync(x => x.Id == schedule.Id);
                if (existing != null)
                {
                    _dbContext.ApiCallSchedules.Remove(existing);
                    removedCount++;
                }

                _recurringJobManager.RemoveIfExists(GetJobId(schedule.Id));
            }
        }

        if (removedCount > 0)
        {
            await _dbContext.SaveChangesAsync();
        }

        return Ok(new { Message = $"Removed {removedCount} schedule(s) for inactive API calls" });
    }

    private async Task<ScheduleResponse> UpsertScheduleAsync(ScheduleRequest request)
    {
        var apiCall = await _apiCallRepository.GetByIdAsync(request.ApiCallId);
        if (apiCall == null)
        {
            throw new InvalidOperationException($"API call with ID {request.ApiCallId} not found");
        }

        if (!TryValidateCron(request.CronExpression, out var error))
        {
            throw new ArgumentException(error);
        }

        var schedule = request.ScheduleId.HasValue
            ? await _dbContext.ApiCallSchedules.FirstOrDefaultAsync(x => x.Id == request.ScheduleId.Value)
            : null;

        var now = DateTime.UtcNow;
        if (schedule == null)
        {
            schedule = new ApiCallSchedule
            {
                Id = request.ScheduleId ?? Guid.NewGuid(),
                ApiCallId = request.ApiCallId,
                CreatedAt = now
            };
            _dbContext.ApiCallSchedules.Add(schedule);
        }

        schedule.ApiCallId = request.ApiCallId;
        schedule.CronExpression = request.CronExpression.Trim();
        schedule.IsEnabled = request.IsEnabled;
        schedule.ModifiedAt = now;

        await _dbContext.SaveChangesAsync();

        var jobId = GetJobId(schedule.Id);
        if (schedule.IsEnabled)
        {
            _recurringJobManager.AddOrUpdate(
                jobId,
                () => _callApiService.ExecuteApiCallAsync(schedule.ApiCallId, schedule.Id),
                schedule.CronExpression);
        }
        else
        {
            _recurringJobManager.RemoveIfExists(jobId);
        }

        return new ScheduleResponse
        {
            ScheduleId = schedule.Id,
            JobId = jobId,
            ApiCallId = schedule.ApiCallId,
            CronExpression = schedule.CronExpression,
            IsEnabled = schedule.IsEnabled,
            Message = schedule.IsEnabled
                ? "API call scheduled successfully"
                : "Schedule saved successfully but remains disabled"
        };
    }

    private static string GetJobId(Guid scheduleId)
    {
        return $"{JobIdPrefix}{scheduleId}";
    }

    private static bool TryGetScheduleId(string value, out Guid scheduleId)
    {
        scheduleId = Guid.Empty;

        if (Guid.TryParse(value, out scheduleId))
        {
            return true;
        }

        if (value.StartsWith(JobIdPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return Guid.TryParse(value[JobIdPrefix.Length..], out scheduleId);
        }

        return false;
    }

    private static IReadOnlyDictionary<string, RecurringJobDto> GetRecurringJobMap()
    {
        using var connection = JobStorage.Current?.GetConnection();
        if (connection == null)
        {
            return new Dictionary<string, RecurringJobDto>(StringComparer.OrdinalIgnoreCase);
        }

        return connection.GetRecurringJobs().ToDictionary(job => job.Id, StringComparer.OrdinalIgnoreCase);
    }

    private static bool TryValidateCron(string? cronExpression, out string? error)
    {
        if (string.IsNullOrWhiteSpace(cronExpression))
        {
            error = "CRON expression is required.";
            return false;
        }

        try
        {
            CronExpression.Parse(cronExpression.Trim(), CronFormat.Standard);
            error = null;
            return true;
        }
        catch (CronFormatException)
        {
            error = "Invalid CRON expression. Use standard 5-part format.";
            return false;
        }
    }

    private static ApiCallLogDto MapToDto(ApiCallLog log)
    {
        return new ApiCallLogDto
        {
            Id = log.Id,
            ApiCallId = log.ApiCallId,
            HttpMethod = log.HttpMethod,
            RequestUri = log.RequestUri,
            RequestHeaders = log.RequestHeaders,
            RequestParameters = log.RequestParameters,
            RequestBody = log.RequestBody,
            ResponseCode = log.ResponseCode,
            ResponseBody = log.ResponseBody,
            ExecutedAt = log.ExecutedAt,
            DurationMs = log.DurationMs,
            Success = log.Success,
            ErrorMessage = log.ErrorMessage
        };
    }
}
