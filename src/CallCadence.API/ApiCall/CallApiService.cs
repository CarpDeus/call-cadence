using System.Diagnostics;
using BugLogger.Interfaces;
using CallCadence.API.Dashboard;
using CallCadence.API.Hubs;
using CallCadence.Application.ApiCall;
using CallCadence.Domain.ApiCall;
using Cronos;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;

namespace CallCadence.Infrastructure.ApiCall;

/// <summary>
/// Service for executing API calls and logging the results.
/// </summary>
public sealed class CallApiService
{
    private const string MaskedSensitiveValue = "***";
    private static readonly HashSet<string> SensitiveHeaderNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Cookie",
        "Set-Cookie",
        "X-API-Key",
        "Api-Key"
    };

    private readonly IApiCallRepository _apiCallRepository;
    private readonly IApiCallLogRepository _logRepository;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ISentryService _sentryService;
    private readonly ApiCallActivityTracker _activityTracker;
    private readonly IHubContext<GenericHub> _hubContext;
    private readonly CallCadenceDbContext _dbContext;
    private readonly IRecurringJobManager _recurringJobManager;

    public CallApiService(
        IApiCallRepository apiCallRepository,
        IApiCallLogRepository logRepository,
        IHttpClientFactory httpClientFactory,
        ISentryService sentryService,
        ApiCallActivityTracker activityTracker,
        IHubContext<GenericHub> hubContext,
        CallCadenceDbContext dbContext,
        IRecurringJobManager recurringJobManager)
    {
        _apiCallRepository = apiCallRepository;
        _logRepository = logRepository;
        _httpClientFactory = httpClientFactory;
        _sentryService = sentryService;
        _activityTracker = activityTracker;
        _hubContext = hubContext;
        _dbContext = dbContext;
        _recurringJobManager = recurringJobManager;
    }

    /// <summary>
    /// Executes an API call by ID. This is called by Hangfire jobs.
    /// </summary>
    public async Task ExecuteApiCallAsync(Guid apiCallId, Guid scheduleId)
    {
        using (SentrySdk.PushScope())
        {
            var title = "Unknown API Call";
            var startedAt = DateTime.UtcNow;

            try
            {
                var apiCall = await _apiCallRepository.GetByIdAsync(apiCallId);

                if (apiCall == null)
                {
                    await UnscheduleApiCallAsync(apiCallId);
                    return;
                }

                title = apiCall.Title;
                if (!apiCall.IsActive)
                {
                    await UnscheduleApiCallAsync(apiCall.Id);
                    return;
                }

                if (await ShouldSkipDueToIntersectingScheduleAsync(apiCallId, scheduleId))
                {
                    return;
                }

                startedAt = DateTime.UtcNow;
                var startedEvent = _activityTracker.MarkStarted(apiCall.Id, apiCall.Title, startedAt);
                await _hubContext.Clients.All.SendAsync("ApiCallStarted", startedEvent);

                await ExecuteAndLogAsync(apiCall, startedAt);
            }
            catch (Exception ex)
            {
                await RegisterImmediateErrorAsync(apiCallId, title, startedAt, $"Unexpected error: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Executes a test API call from the provided definition without saving to the database.
    /// </summary>
    public async Task<TestApiCallResponse> TestApiCallAsync(TestApiCallRequest request)
    {
        var stopwatch = Stopwatch.StartNew();
        var response = new TestApiCallResponse();

        try
        {
            var httpClient = _httpClientFactory.CreateClient();

            var endpointUrl = request.EndpointUrl;
            var processedParameters = request.Parameters
                .Where(p => !string.IsNullOrWhiteSpace(p.Name))
                .Select(p => new NamedValue
                {
                    Name = p.Name,
                    Value = p.Value == null ? null : MacroSubstitutionProcessor.Process(p.Value)
                })
                .ToList();

            if (processedParameters.Count > 0)
            {
                var paramPairs = processedParameters.Select(p =>
                    $"{Uri.EscapeDataString(p.Name ?? string.Empty)}{(p.Value != null ? "=" + Uri.EscapeDataString(p.Value) : string.Empty)}");
                var queryString = string.Join("&", paramPairs);
                if (!string.IsNullOrEmpty(queryString))
                {
                    endpointUrl = endpointUrl.Contains('?')
                        ? $"{endpointUrl}&{queryString}"
                        : $"{endpointUrl}?{queryString}";
                }
            }

            var httpRequest = new HttpRequestMessage(new HttpMethod(request.HttpMethod), endpointUrl);
            var processedHeaders = new List<NamedValue>();

            foreach (var header in request.Headers)
            {
                if (!string.IsNullOrWhiteSpace(header.Name))
                {
                    var headerValue = header.Value == null ? null : MacroSubstitutionProcessor.Process(header.Value);
                    httpRequest.Headers.TryAddWithoutValidation(header.Name, headerValue);
                    processedHeaders.Add(new NamedValue
                    {
                        Name = header.Name,
                        Value = IsSensitiveHeader(header.Name) ? MaskedSensitiveValue : headerValue
                    });
                }
            }

            string? processedPayload = null;
            if (!string.IsNullOrWhiteSpace(request.Payload) &&
                (request.HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase) ||
                 request.HttpMethod.Equals("PUT", StringComparison.OrdinalIgnoreCase) ||
                 request.HttpMethod.Equals("PATCH", StringComparison.OrdinalIgnoreCase)))
            {
                processedPayload = MacroSubstitutionProcessor.Process(request.Payload);
                httpRequest.Content = new StringContent(
                    processedPayload,
                    System.Text.Encoding.UTF8,
                    "application/json");
            }

            response.RequestUri = endpointUrl;
            response.RequestHeaders = processedHeaders;
            response.RequestParameters = processedParameters;
            response.RequestBody = processedPayload;

            var httpResponse = await httpClient.SendAsync(httpRequest);
            stopwatch.Stop();

            response.ResponseCode = (int)httpResponse.StatusCode;
            response.ResponseBody = await httpResponse.Content.ReadAsStringAsync();
            response.Success = httpResponse.IsSuccessStatusCode;
            response.DurationMs = stopwatch.ElapsedMilliseconds;

            if (!response.Success)
            {
                response.ErrorMessage = $"HTTP {response.ResponseCode} returned.";
            }
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            response.ResponseCode = 0;
            response.Success = false;
            response.ErrorMessage = ex.Message;
            response.DurationMs = stopwatch.ElapsedMilliseconds;
        }

        return response;
    }

    private async Task ExecuteAndLogAsync(Domain.ApiCall.ApiCall apiCall, DateTime startedAt)
    {
        var stopwatch = Stopwatch.StartNew();
        var log = new ApiCallLog
        {
            Id = Guid.NewGuid(),
            ApiCallId = apiCall.Id,
            HttpMethod = apiCall.HttpMethod,
            ExecutedAt = DateTime.UtcNow
        };

        using (SentrySdk.PushScope())
        {
            try
            {
                var httpClient = _httpClientFactory.CreateClient();

                // Build URL with query parameters if present
                var endpointUrl = apiCall.EndpointUrl;
                var processedParameters = apiCall.Parameters
                    .Where(p => !string.IsNullOrWhiteSpace(p.Name))
                    .Select(p => new NamedValue
                    {
                        Name = p.Name,
                        Value = p.Value == null ? null : MacroSubstitutionProcessor.Process(p.Value)
                    })
                    .ToList();

                if (processedParameters.Count > 0)
                {
                    var paramPairs = processedParameters.Select(p =>
                        $"{Uri.EscapeDataString(p.Name ?? string.Empty)}{(p.Value != null ? "=" + Uri.EscapeDataString(p.Value) : string.Empty)}");
                    var queryString = string.Join("&", paramPairs);
                    if (!string.IsNullOrEmpty(queryString))
                    {
                        endpointUrl = endpointUrl.Contains('?')
                            ? $"{endpointUrl}&{queryString}"
                            : $"{endpointUrl}?{queryString}";
                    }
                }

                var request = new HttpRequestMessage(new HttpMethod(apiCall.HttpMethod), endpointUrl);
                var processedHeaders = new List<NamedValue>();

                // Add headers if present
                foreach (var header in apiCall.Headers)
                {
                    if (!string.IsNullOrWhiteSpace(header.Name))
                    {
                        var headerValue = header.Value == null ? null : MacroSubstitutionProcessor.Process(header.Value);
                        request.Headers.TryAddWithoutValidation(header.Name, headerValue);
                        processedHeaders.Add(new NamedValue
                        {
                            Name = header.Name,
                            Value = IsSensitiveHeader(header.Name) ? MaskedSensitiveValue : headerValue
                        });
                    }
                }

                string? processedPayload = null;
                // Add payload for POST/PUT/PATCH requests
                if (!string.IsNullOrWhiteSpace(apiCall.Payload) &&
                    (apiCall.HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase) ||
                     apiCall.HttpMethod.Equals("PUT", StringComparison.OrdinalIgnoreCase) ||
                     apiCall.HttpMethod.Equals("PATCH", StringComparison.OrdinalIgnoreCase)))
                {
                    processedPayload = MacroSubstitutionProcessor.Process(apiCall.Payload);
                    request.Content = new StringContent(
                        processedPayload,
                        System.Text.Encoding.UTF8,
                        "application/json");
                }

                log.RequestUri = endpointUrl;
                log.RequestHeaders = processedHeaders;
                log.RequestParameters = processedParameters;
                log.RequestBody = processedPayload;

                var response = await httpClient.SendAsync(request);
                stopwatch.Stop();

                log.ResponseCode = (int)response.StatusCode;
                log.ResponseBody = await response.Content.ReadAsStringAsync();
                log.Success = response.IsSuccessStatusCode;
                log.DurationMs = stopwatch.ElapsedMilliseconds;

                if (!log.Success)
                {
                    log.ErrorMessage = $"HTTP {log.ResponseCode} returned.";
                }
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                log.ResponseCode = 0;
                log.Success = false;
                log.ErrorMessage = ex.Message;
                log.DurationMs = stopwatch.ElapsedMilliseconds;
            }
        }

        var completedAt = DateTime.UtcNow;
        var completionEvent = _activityTracker.MarkCompleted(
            apiCall.Id,
            apiCall.Title,
            startedAt,
            completedAt,
            log.Success,
            log.ErrorMessage);

        await _hubContext.Clients.All.SendAsync("ApiCallCompleted", completionEvent);
        await _logRepository.CreateAsync(log);
    }

    private async Task RegisterImmediateErrorAsync(Guid apiCallId, string title, DateTime startedAt, string errorMessage)
    {
        await LogErrorAsync(apiCallId, errorMessage);

        var completionEvent = _activityTracker.MarkCompleted(
            apiCallId,
            title,
            startedAt,
            DateTime.UtcNow,
            success: false,
            errorMessage);

        await _hubContext.Clients.All.SendAsync("ApiCallCompleted", completionEvent);
    }

    private async Task LogErrorAsync(Guid apiCallId, string errorMessage)
    {
        var log = new ApiCallLog
        {
            Id = Guid.NewGuid(),
            ApiCallId = apiCallId,
            ExecutedAt = DateTime.UtcNow,
            ResponseCode = 0,
            Success = false,
            ErrorMessage = errorMessage,
            DurationMs = 0
        };

        var sentryTag = apiCallId.ToString();
        _sentryService.LogException(new Exception(errorMessage), errorMessage, sentryTag);
        await _logRepository.CreateAsync(log);
    }

    private async Task UnscheduleApiCallAsync(Guid apiCallId)
    {
        var schedules = await _dbContext.ApiCallSchedules
            .Where(schedule => schedule.ApiCallId == apiCallId)
            .ToListAsync();
        if (schedules.Count == 0)
        {
            return;
        }

        _dbContext.ApiCallSchedules.RemoveRange(schedules);
        await _dbContext.SaveChangesAsync();

        foreach (var schedule in schedules)
        {
            _recurringJobManager.RemoveIfExists($"schedule-{schedule.Id}");
        }
    }

    private static bool IsSensitiveHeader(string headerName)
    {
        if (SensitiveHeaderNames.Contains(headerName))
        {
            return true;
        }

        return headerName.Contains("authorization", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<bool> ShouldSkipDueToIntersectingScheduleAsync(Guid apiCallId, Guid scheduleId)
    {
        var schedules = await _dbContext.ApiCallSchedules
            .Where(s => s.ApiCallId == apiCallId && s.IsEnabled)
            .ToListAsync();

        if (schedules.Count <= 1)
        {
            return false;
        }

        var mostFrequentId = GetMostFrequentScheduleId(schedules);

        if (scheduleId == mostFrequentId)
        {
            return false;
        }

        // Skip this schedule only if the most frequent schedule also fires in the same minute window
        var mostFrequentSchedule = schedules.First(s => s.Id == mostFrequentId);
        var now = DateTime.UtcNow;
        var minuteStart = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0, DateTimeKind.Utc);
        var minuteEnd = minuteStart.AddMinutes(1);

        return FiresInWindow(mostFrequentSchedule.CronExpression, minuteStart, minuteEnd);
    }

    private static Guid GetMostFrequentScheduleId(IList<ApiCallSchedule> schedules)
    {
        return schedules
            .Select(s => new { s.Id, Interval = GetApproximateInterval(s.CronExpression) })
            .OrderBy(x => x.Interval)
            .First()
            .Id;
    }

    private static TimeSpan GetApproximateInterval(string cronExpression)
    {
        try
        {
            var expr = CronExpression.Parse(cronExpression, CronFormat.Standard);
            var reference = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var first = expr.GetNextOccurrence(reference, TimeZoneInfo.Utc);
            if (!first.HasValue) return TimeSpan.MaxValue;
            var second = expr.GetNextOccurrence(first.Value, TimeZoneInfo.Utc);
            if (!second.HasValue) return TimeSpan.MaxValue;
            return second.Value - first.Value;
        }
        catch
        {
            return TimeSpan.MaxValue;
        }
    }

    private static bool FiresInWindow(string cronExpression, DateTime windowStart, DateTime windowEnd)
    {
        try
        {
            var expr = CronExpression.Parse(cronExpression, CronFormat.Standard);
            var next = expr.GetNextOccurrence(windowStart, TimeZoneInfo.Utc);
            return next.HasValue && next.Value < windowEnd;
        }
        catch
        {
            return false;
        }
    }
}
