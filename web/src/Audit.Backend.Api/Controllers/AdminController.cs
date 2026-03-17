using Audit.Backend.Api.Services;
using Audit.Backend.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace Audit.Backend.Api.Controllers;

[ApiController]
[Route("api/v1/admin")]
public sealed class AdminController(IEnrollmentStore store) : ControllerBase
{
    [HttpGet("devices")]
    [ProducesResponseType(typeof(IReadOnlyList<DeviceSummary>), StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<DeviceSummary>> GetDevices([FromQuery] string? q, [FromQuery] string? status)
    {
        return Ok(store.ListDevices(q, status));
    }
}
