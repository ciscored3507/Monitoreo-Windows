using Audit.Backend.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Audit.Backend.Api.Controllers;

[ApiController]
[Route("api/v1/evidence")]
public sealed class EvidenceController(IEnrollmentStore store) : ControllerBase
{
    [HttpGet("sessions")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult GetSessions([FromQuery] Guid device_id, [FromQuery] DateTimeOffset from_utc, [FromQuery] DateTimeOffset to_utc)
    {
        if (device_id == Guid.Empty || from_utc == default || to_utc == default || from_utc >= to_utc)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid query", detail: "Provide valid device_id, from_utc and to_utc.");
        }

        return Ok(store.ListSessions(device_id, from_utc, to_utc));
    }
}
