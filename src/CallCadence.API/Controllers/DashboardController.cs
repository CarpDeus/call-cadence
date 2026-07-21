using CallCadence.API.Dashboard;
using CallCadence.Application.Dashboard;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CallCadence.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class DashboardController : ControllerBase
{
    private readonly ApiCallActivityTracker _activityTracker;

    public DashboardController(ApiCallActivityTracker activityTracker)
    {
        _activityTracker = activityTracker;
    }

    [AllowAnonymous]
    [HttpGet("state")]
    public ActionResult<DashboardStateDto> GetState()
    {
        return Ok(_activityTracker.GetState());
    }

    [Authorize]
    [HttpPost("errors/clear")]
    public ActionResult ClearErrors([FromBody] ClearDashboardErrorsRequest request)
    {
        _activityTracker.ClearErrors(request.ErrorIds);
        return Ok();
    }
}
