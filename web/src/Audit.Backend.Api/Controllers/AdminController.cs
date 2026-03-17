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

    [HttpGet("devices/{deviceId:guid}/certificates")]
    [ProducesResponseType(typeof(IReadOnlyList<DeviceCertificateDto>), StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<DeviceCertificateDto>> GetDeviceCertificates(Guid deviceId)
    {
        return Ok(store.ListCertificates(deviceId));
    }

    [HttpPost("devices/{deviceId:guid}/certificates/bind")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public IActionResult BindCertificate(Guid deviceId, [FromBody] BindCertificateRequest request)
    {
        if (deviceId == Guid.Empty || string.IsNullOrWhiteSpace(request.ThumbprintSha256))
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid request", detail: "deviceId and thumbprint are required");
        }

        store.BindCertificate(deviceId, request.ThumbprintSha256);
        return NoContent();
    }

    [HttpPost("devices/{deviceId:guid}/certificates/revoke")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult RevokeCertificate(Guid deviceId, [FromBody] RevokeCertificateRequest request)
    {
        var revoked = store.RevokeCertificate(deviceId, request.ThumbprintSha256, request.Reason);
        if (!revoked)
        {
            return NotFound();
        }

        return NoContent();
    }

    [HttpPost("devices/{deviceId:guid}/certificates/rotate/start")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public IActionResult StartRotation(Guid deviceId, [FromBody] StartRotationRequest request)
    {
        if (deviceId == Guid.Empty || string.IsNullOrWhiteSpace(request.NewThumbprintSha256))
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid request", detail: "deviceId and new thumbprint are required");
        }

        store.SetRotationInstruction(deviceId, request.NewThumbprintSha256, request.GraceSeconds);
        return NoContent();
    }
}
