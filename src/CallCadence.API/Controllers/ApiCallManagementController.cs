using CallCadence.Application.ApiCall;
using CallCadence.Domain.ApiCall;
using CallCadence.Domain.Paging;
using Hangfire;
using Hangfire.Storage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CallCadence.Infrastructure.ApiCall;

namespace CallCadence.API.Controllers;

/// <summary>
/// Controller for managing API call definitions.
/// </summary>
[ApiController]
[Authorize]
[Route("api/[controller]")]
public sealed class ApiCallManagementController : ControllerBase
{
    private readonly ApiCallManagementService _managementService;
    private readonly IApiCallRepository _apiCallRepository;
    private readonly CallCadenceDbContext _dbContext;
    private readonly IRecurringJobManager _recurringJobManager;
    private readonly CallApiService _callApiService;

    public ApiCallManagementController(
        ApiCallManagementService managementService,
        IApiCallRepository apiCallRepository,
        CallCadenceDbContext dbContext,
        IRecurringJobManager recurringJobManager,
        CallApiService callApiService)
    {
        _managementService = managementService;
        _apiCallRepository = apiCallRepository;
        _dbContext = dbContext;
        _recurringJobManager = recurringJobManager;
        _callApiService = callApiService;
    }

    /// <summary>
    /// Get all API calls.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<ApiCallDto>>> GetAll()
    {
        var apiCalls = await _managementService.GetAllAsync();
        return Ok(apiCalls);
    }

    /// <summary>
    /// Get a paged list of API calls for the API editing screen.
    /// </summary>
    [HttpGet("list")]
    public async Task<ActionResult<PagedResult<ApiCallListItemDto>>> GetList([FromQuery] ApiCallListRequest request)
    {
        var pageNumber = request.PageNumber < 1 ? 1 : request.PageNumber;
        var pageSize = request.PageSize <= 0 ? 10 : Math.Min(request.PageSize, 100);
        var sortBy = string.IsNullOrWhiteSpace(request.SortBy) ? "title" : request.SortBy.Trim();
        var scheduleLookup = await BuildScheduleLookupAsync();

        if (RequiresScheduleSorting(sortBy))
        {
            var items = ApplyScheduleMetadata(
                    await _apiCallRepository.GetListItemsAsync(request.Enabled),
                    scheduleLookup)
                .ToList();
            var orderedItems = ApplySorting(items, sortBy, request.SortDescending).ToList();
            var totalItems = orderedItems.Count;
            var pageItems = orderedItems
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToList();
            var pageCount = totalItems == 0 ? 0 : (int)Math.Ceiling(totalItems / (double)pageSize);

            return Ok(new PagedResult<ApiCallListItemDto>(
                new Paging(pageCount, totalItems, pageNumber, pageSize),
                pageItems));
        }

        var pagedResult = await _apiCallRepository.GetListPageAsync(new ApiCallListRequest
        {
            PageNumber = pageNumber,
            PageSize = pageSize,
            SortBy = sortBy,
            SortDescending = request.SortDescending,
            Enabled = request.Enabled
        });

        return Ok(new PagedResult<ApiCallListItemDto>(
            pagedResult.Paging,
            ApplyScheduleMetadata(pagedResult.Items, scheduleLookup).ToList()));
    }

    /// <summary>
    /// Get an API call by ID.
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<ApiCallDto>> GetById(Guid id)
    {
        var apiCall = await _managementService.GetByIdAsync(id);
        if (apiCall == null)
        {
            return NotFound();
        }
        return Ok(apiCall);
    }

    /// <summary>
    /// Create a new API call.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<ApiCallDto>> Create([FromBody] CreateApiCallDto dto)
    {
        try
        {
            var created = await _managementService.CreateAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Create multiple API calls.
    /// </summary>
    [HttpPost("bulk")]
    public async Task<ActionResult<IEnumerable<ApiCallDto>>> CreateMany([FromBody] IEnumerable<CreateApiCallDto> dtos)
    {
        try
        {
            var created = await _managementService.CreateManyAsync(dtos);
            return Ok(created);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Test an API call definition without saving it.
    /// </summary>
    [HttpPost("test")]
    public async Task<ActionResult<TestApiCallResponse>> Test([FromBody] TestApiCallRequest request)
    {
        var result = await _callApiService.TestApiCallAsync(request);
        return Ok(result);
    }

    /// <summary>
    /// Update an existing API call.
    /// </summary>
    [HttpPut("{id}")]
    public async Task<ActionResult<ApiCallDto>> Update(Guid id, [FromBody] UpdateApiCallDto dto)
    {
        if (id != dto.Id)
        {
            return BadRequest("ID in URL does not match ID in body");
        }

        try
        {
            var updated = await _managementService.UpdateAsync(dto);
            if (!updated.IsActive)
            {
                await RemoveSchedulesForApiCallAsync(updated.Id);
            }
            return Ok(updated);
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
    /// Update multiple API calls.
    /// </summary>
    [HttpPut("bulk")]
    public async Task<ActionResult<IEnumerable<ApiCallDto>>> UpdateMany([FromBody] IEnumerable<UpdateApiCallDto> dtos)
    {
        try
        {
            var updated = await _managementService.UpdateManyAsync(dtos);
            foreach (var apiCall in updated.Where(apiCall => !apiCall.IsActive))
            {
                await RemoveSchedulesForApiCallAsync(apiCall.Id);
            }
            return Ok(updated);
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
    /// Activate an API call.
    /// </summary>
    [HttpPost("{id}/Activate")]
    public async Task<ActionResult> Activate(Guid id)
    {
        try
        {
            await _managementService.ActivateAsync(id);
            return Ok();
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ex.Message);
        }
    }

    /// <summary>
    /// Activate multiple API calls.
    /// </summary>
    [HttpPost("bulk/Activate")]
    public async Task<ActionResult> ActivateMany([FromBody] IEnumerable<Guid> ids)
    {
        try
        {
            await _managementService.ActivateManyAsync(ids);
            return Ok();
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ex.Message);
        }
    }

    /// <summary>
    /// Deactivate an API call.
    /// </summary>
    [HttpPost("{id}/Deactivate")]
    public async Task<ActionResult> Deactivate(Guid id)
    {
        try
        {
            await _managementService.DeactivateAsync(id);
            await RemoveSchedulesForApiCallAsync(id);
            return Ok();
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ex.Message);
        }
    }

    /// <summary>
    /// Deactivate multiple API calls.
    /// </summary>
    [HttpPost("bulk/Deactivate")]
    public async Task<ActionResult> DeactivateMany([FromBody] IEnumerable<Guid> ids)
    {
        try
        {
            await _managementService.DeactivateManyAsync(ids);
            foreach (var id in ids.Distinct())
            {
                await RemoveSchedulesForApiCallAsync(id);
            }
            return Ok();
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ex.Message);
        }
    }

    private static IEnumerable<ApiCallListItemDto> ApplySorting(
        IEnumerable<ApiCallListItemDto> items,
        string sortBy,
        bool sortDescending)
    {
        return (sortBy.ToLowerInvariant(), sortDescending) switch
        {
            ("enabled", false) => items.OrderBy(item => item.IsActive).ThenBy(item => item.Title, StringComparer.OrdinalIgnoreCase),
            ("enabled", true) => items.OrderByDescending(item => item.IsActive).ThenBy(item => item.Title, StringComparer.OrdinalIgnoreCase),
            ("hasschedule", false) => items.OrderBy(item => item.HasSchedule).ThenBy(item => item.Title, StringComparer.OrdinalIgnoreCase),
            ("hasschedule", true) => items.OrderByDescending(item => item.HasSchedule).ThenBy(item => item.Title, StringComparer.OrdinalIgnoreCase),
            ("nextscheduledcall", false) => items
                .OrderBy(item => item.NextScheduledCall is null ? 1 : 0)
                .ThenBy(item => item.NextScheduledCall)
                .ThenBy(item => item.Title, StringComparer.OrdinalIgnoreCase),
            ("nextscheduledcall", true) => items
                .OrderBy(item => item.NextScheduledCall is null ? 1 : 0)
                .ThenByDescending(item => item.NextScheduledCall)
                .ThenBy(item => item.Title, StringComparer.OrdinalIgnoreCase),
            ("lastsuccessat", false) => items
                .OrderBy(item => item.LastSuccessAt is null ? 1 : 0)
                .ThenBy(item => item.LastSuccessAt)
                .ThenBy(item => item.Title, StringComparer.OrdinalIgnoreCase),
            ("lastsuccessat", true) => items
                .OrderBy(item => item.LastSuccessAt is null ? 1 : 0)
                .ThenByDescending(item => item.LastSuccessAt)
                .ThenBy(item => item.Title, StringComparer.OrdinalIgnoreCase),
            ("lasterrorat", false) => items
                .OrderBy(item => item.LastErrorAt is null ? 1 : 0)
                .ThenBy(item => item.LastErrorAt)
                .ThenBy(item => item.Title, StringComparer.OrdinalIgnoreCase),
            ("lasterrorat", true) => items
                .OrderBy(item => item.LastErrorAt is null ? 1 : 0)
                .ThenByDescending(item => item.LastErrorAt)
                .ThenBy(item => item.Title, StringComparer.OrdinalIgnoreCase),
            (_, true) => items.OrderByDescending(item => item.Title, StringComparer.OrdinalIgnoreCase),
            _ => items.OrderBy(item => item.Title, StringComparer.OrdinalIgnoreCase)
        };
    }

    private static IEnumerable<ApiCallListItemDto> ApplyScheduleMetadata(
        IEnumerable<ApiCallListItemDto> items,
        IReadOnlyDictionary<Guid, ScheduleMetadata> scheduleLookup)
    {
        foreach (var item in items)
        {
            if (scheduleLookup.TryGetValue(item.Id, out var scheduleMetadata))
            {
                item.HasSchedule = scheduleMetadata.HasSchedule;
                item.NextScheduledCall = scheduleMetadata.NextScheduledCall;
            }
            else
            {
                item.HasSchedule = false;
                item.NextScheduledCall = null;
            }
            yield return item;
        }
    }

    private static bool RequiresScheduleSorting(string sortBy)
    {
        return sortBy.Equals("hasschedule", StringComparison.OrdinalIgnoreCase)
            || sortBy.Equals("nextscheduledcall", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<IReadOnlyDictionary<Guid, ScheduleMetadata>> BuildScheduleLookupAsync()
    {
        var schedules = await _dbContext.ApiCallSchedules.AsNoTracking().ToListAsync();
        if (schedules.Count == 0)
        {
            return new Dictionary<Guid, ScheduleMetadata>();
        }

        var recurringJobs = GetRecurringJobMap();
        var lookup = new Dictionary<Guid, ScheduleMetadata>();

        foreach (var groupedSchedules in schedules.GroupBy(schedule => schedule.ApiCallId))
        {
            lookup[groupedSchedules.Key] = new ScheduleMetadata(
                HasSchedule: true,
                NextScheduledCall: GetNextScheduledCall(groupedSchedules, recurringJobs));
        }

        return lookup;
    }

    private static DateTime? GetNextScheduledCall(
        IEnumerable<ApiCallSchedule> schedules,
        IReadOnlyDictionary<string, DateTime?> recurringJobs)
    {
        DateTime? nextScheduledCall = null;

        foreach (var schedule in schedules)
        {
            if (!schedule.IsEnabled)
            {
                continue;
            }

            if (!recurringJobs.TryGetValue(GetJobId(schedule.Id), out var nextExecution) || !nextExecution.HasValue)
            {
                continue;
            }

            if (!nextScheduledCall.HasValue || nextExecution.Value < nextScheduledCall.Value)
            {
                nextScheduledCall = nextExecution;
            }
        }

        return nextScheduledCall;
    }

    private static IReadOnlyDictionary<string, DateTime?> GetRecurringJobMap()
    {
        using var connection = JobStorage.Current?.GetConnection();
        if (connection is null)
        {
            return new Dictionary<string, DateTime?>(StringComparer.OrdinalIgnoreCase);
        }

        return connection.GetRecurringJobs().ToDictionary(job => job.Id, job => job.NextExecution, StringComparer.OrdinalIgnoreCase);
    }

    private static string GetJobId(Guid scheduleId)
    {
        return $"schedule-{scheduleId}";
    }

    private async Task RemoveSchedulesForApiCallAsync(Guid apiCallId)
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
            _recurringJobManager.RemoveIfExists(GetJobId(schedule.Id));
        }
    }

    private sealed record ScheduleMetadata(bool HasSchedule, DateTime? NextScheduledCall);
}
