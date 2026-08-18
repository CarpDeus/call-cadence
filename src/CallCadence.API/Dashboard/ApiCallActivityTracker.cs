using System.Collections.Concurrent;
using CallCadence.Application.Dashboard;

namespace CallCadence.API.Dashboard;

public sealed class ApiCallActivityTracker
{
    private const int MaxTrackedErrors = 500;
    private const int MaxRecentSuccessfulCalls = 5;
    private readonly ConcurrentDictionary<Guid, ActiveApiCallDto> _activeCalls = new();
    private readonly ConcurrentDictionary<Guid, DashboardErrorDto> _errors = new();
    private readonly ConcurrentQueue<Guid> _errorOrder = new();
    private readonly object _recentSuccessLock = new();
    private readonly LinkedList<RecentSuccessfulCallDto> _recentSuccessfulCalls = new();
    private long _successfulCalls;
    private long _errorCount;
    private DateTime? _lastSuccessfulCallAt;
    private DateTime? _lastErroredCallAt;

    public DateTime StartedAt { get; } = DateTime.UtcNow;

    public ActiveApiCallDto MarkStarted(Guid apiCallId, string title, DateTime startedAt)
    {
        var activity = new ActiveApiCallDto
        {
            ApiCallId = apiCallId,
            Title = title,
            StartedAt = startedAt
        };

        _activeCalls[apiCallId] = activity;
        return activity;
    }

    public DashboardCallCompletedDto MarkCompleted(Guid apiCallId, string title, DateTime startedAt, DateTime completedAt, bool success, string? errorMessage)
    {
        _activeCalls.TryRemove(apiCallId, out _);

        DashboardErrorDto? dashboardError = null;
        if (success)
        {
            Interlocked.Increment(ref _successfulCalls);

            var recentCall = new RecentSuccessfulCallDto
            {
                ApiCallId = apiCallId,
                Title = title,
                CompletedAt = completedAt
            };

            lock (_recentSuccessLock)
            {
                _lastSuccessfulCallAt = completedAt;
                _recentSuccessfulCalls.AddFirst(recentCall);
                while (_recentSuccessfulCalls.Count > MaxRecentSuccessfulCalls)
                {
                    _recentSuccessfulCalls.RemoveLast();
                }
            }
        }
        else
        {
            Interlocked.Increment(ref _errorCount);

            dashboardError = new DashboardErrorDto
            {
                Id = Guid.NewGuid(),
                ApiCallId = apiCallId,
                Title = title,
                ErrorMessage = errorMessage ?? "Unknown error",
                OccurredAt = completedAt
            };

            lock (_recentSuccessLock)
            {
                _lastErroredCallAt = completedAt;
            }

            _errors[dashboardError.Id] = dashboardError;
            _errorOrder.Enqueue(dashboardError.Id);

            while (_errors.Count > MaxTrackedErrors && _errorOrder.TryDequeue(out var staleErrorId))
            {
                _errors.TryRemove(staleErrorId, out _);
            }
        }

        return new DashboardCallCompletedDto
        {
            ApiCallId = apiCallId,
            Title = title,
            StartedAt = startedAt,
            CompletedAt = completedAt,
            Success = success,
            ErrorMessage = errorMessage,
            Error = dashboardError
        };
    }

    public DashboardStateDto GetState()
    {
        List<RecentSuccessfulCallDto> recentSuccessfulCalls;
        DateTime? lastSuccessfulCallAt;
        DateTime? lastErroredCallAt;
        lock (_recentSuccessLock)
        {
            recentSuccessfulCalls = [.. _recentSuccessfulCalls];
            lastSuccessfulCallAt = _lastSuccessfulCallAt;
            lastErroredCallAt = _lastErroredCallAt;
        }

        return new DashboardStateDto
        {
            CurrentActivities = _activeCalls.Values.OrderBy(x => x.StartedAt).ToList(),
            SuccessfulCalls = Interlocked.Read(ref _successfulCalls),
            ErrorCount = Interlocked.Read(ref _errorCount),
            ServerStartedAt = StartedAt,
            RecentSuccessfulCalls = recentSuccessfulCalls,
            Errors = _errors.Values.OrderByDescending(x => x.OccurredAt).ToList()
        };
    }

    public void ClearErrors(IEnumerable<Guid> errorIds)
    {
        foreach (var errorId in errorIds)
        {
            _errors.TryRemove(errorId, out _);
        }
    }

    public void ClearAllErrors()
    {
        _errors.Clear();
    }
}
