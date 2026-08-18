using CallCadence.API.Dashboard;
using CallCadence.Application.Dashboard;
using CallCadence.Infrastructure.ApiCall;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CallCadence.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class DashboardController : ControllerBase
{
    private readonly ApiCallActivityTracker _activityTracker;
    private readonly CallCadenceDbContext _dbContext;

    public DashboardController(ApiCallActivityTracker activityTracker, CallCadenceDbContext dbContext)
    {
        _activityTracker = activityTracker;
        _dbContext = dbContext;
    }

    [AllowAnonymous]
    [HttpGet("state")]
    public async Task<ActionResult<DashboardStateDto>> GetState()
    {
        var state = _activityTracker.GetState();

        var stats = await _dbContext.ApiCallStats.FindAsync(1);
        if (stats != null)
        {
            state.TotalApiCalls = stats.TotalApiCalls;
            state.TotalSuccessfulCalls = stats.TotalSuccessfulCalls;
            state.LastSuccessfulCallAt = stats.LastSuccessfulCallAt;
            state.TotalErroredCalls = stats.TotalErroredCalls;
            state.LastErroredCallAt = stats.LastErroredCallAt;
            state.FirstApiCallAt = stats.FirstApiCallAt;
        }

        return Ok(state);
    }

    [Authorize]
    [HttpPost("errors/clear")]
    public ActionResult ClearErrors([FromBody] ClearDashboardErrorsRequest request)
    {
        _activityTracker.ClearErrors(request.ErrorIds);
        return Ok();
    }

    [Authorize]
    [HttpPost("errors/clear-all")]
    public ActionResult ClearAllErrors()
    {
        _activityTracker.ClearAllErrors();
        return Ok();
    }
}
