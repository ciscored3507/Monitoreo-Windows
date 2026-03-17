using Audit.Backend.Api.Services;
using Audit.Backend.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace Audit.Backend.Api.Controllers;

[ApiController]
[Route("api/v1/agent")]
public sealed class AgentController(IEnrollmentStore store) : ControllerBase
{
    [HttpPost("enroll")]
    [ProducesResponseType(typeof(EnrollResponse), StatusCodes.Status201Created)]
    public IActionResult Enroll([FromBody] EnrollRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Hostname) || string.IsNullOrWhiteSpace(request.DeviceFingerprint))
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid request", detail: "hostname and device_fingerprint are required");
        }

        var response = store.Enroll(request);
        return StatusCode(StatusCodes.Status201Created, response);
    }

    [HttpGet("policy")]
    [ProducesResponseType(typeof(PolicyDto), StatusCodes.Status200OK)]
    public ActionResult<PolicyDto> GetPolicy([FromHeader(Name = "X-Device-Id")] Guid? deviceId)
    {
        if (deviceId is null || deviceId == Guid.Empty)
        {
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Unauthorized", detail: "Missing X-Device-Id header");
        }

        return Ok(store.GetPolicy(deviceId.Value));
    }

    [HttpPost("heartbeat")]
    [ProducesResponseType(typeof(HeartbeatResponse), StatusCodes.Status200OK)]
    public ActionResult<HeartbeatResponse> Heartbeat([FromBody] HeartbeatRequest request, [FromHeader(Name = "X-Device-Id")] Guid? deviceId)
    {
        if (deviceId is null || deviceId == Guid.Empty)
        {
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Unauthorized", detail: "Missing X-Device-Id header");
        }

        store.Heartbeat(deviceId.Value, request);
        return Ok(new HeartbeatResponse(DateTimeOffset.UtcNow));
    }
}
