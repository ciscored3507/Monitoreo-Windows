using Audit.Backend.Api.Services;
using Audit.Backend.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.Certificate;
using Microsoft.AspNetCore.Mvc;

namespace Audit.Backend.Api.Controllers;

[ApiController]
[Route("api/v1/agent")]
[Authorize(AuthenticationSchemes = CertificateAuthenticationDefaults.AuthenticationScheme)]
public sealed class AgentController(IEnrollmentStore store) : ControllerBase
{
    [HttpPost("enroll")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(EnrollResponse), StatusCodes.Status201Created)]
    public IActionResult Enroll([FromBody] EnrollRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Hostname) || string.IsNullOrWhiteSpace(request.DeviceFingerprint))
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid request", detail: "hostname and device_fingerprint are required");
        }

        var response = store.Enroll(request);
        var thumbprint = User.FindFirst("cert_thumbprint")?.Value ?? HttpContext.Connection.ClientCertificate?.Thumbprint;
        if (!string.IsNullOrWhiteSpace(thumbprint))
        {
            store.BindCertificate(response.DeviceId, thumbprint);
        }

        return StatusCode(StatusCodes.Status201Created, response);
    }

    [HttpGet("policy")]
    [ProducesResponseType(typeof(PolicyDto), StatusCodes.Status200OK)]
    public ActionResult<PolicyDto> GetPolicy()
    {
        if (!Guid.TryParse(User.FindFirst("device_id")?.Value, out var deviceId) || deviceId == Guid.Empty)
        {
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Unauthorized", detail: "Missing device claim");
        }

        return Ok(store.GetPolicy(deviceId));
    }

    [HttpPost("heartbeat")]
    [ProducesResponseType(typeof(HeartbeatResponse), StatusCodes.Status200OK)]
    public ActionResult<HeartbeatResponse> Heartbeat([FromBody] HeartbeatRequest request)
    {
        if (!Guid.TryParse(User.FindFirst("device_id")?.Value, out var deviceId) || deviceId == Guid.Empty)
        {
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Unauthorized", detail: "Missing device claim");
        }

        store.Heartbeat(deviceId, request);
        var rotation = store.GetRotationInstruction(deviceId);
        return Ok(new HeartbeatResponse(DateTimeOffset.UtcNow, rotation));
    }

    [HttpPost("certificate/rotation/start")]
    [ProducesResponseType(typeof(StartRotationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public ActionResult<StartRotationResponse> StartRotation([FromBody] StartRotationRequest request)
    {
        if (!Guid.TryParse(User.FindFirst("device_id")?.Value, out var deviceId) || deviceId == Guid.Empty)
        {
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Unauthorized", detail: "Missing device claim");
        }

        var oldThumbprint = User.FindFirst("cert_thumbprint")?.Value ?? HttpContext.Connection.ClientCertificate?.Thumbprint;
        if (string.IsNullOrWhiteSpace(oldThumbprint) || string.IsNullOrWhiteSpace(request.NewThumbprintSha256))
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid request", detail: "Old and new thumbprints are required");
        }

        var response = store.BeginCertificateRotation(deviceId, oldThumbprint, request.NewThumbprintSha256, request.GraceSeconds);
        return Ok(response);
    }

    [HttpPost("certificate/rotation/confirm")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public IActionResult ConfirmRotation([FromBody] ConfirmRotationRequest request)
    {
        if (!Guid.TryParse(User.FindFirst("device_id")?.Value, out var deviceId) || deviceId == Guid.Empty)
        {
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Unauthorized", detail: "Missing device claim");
        }

        if (string.IsNullOrWhiteSpace(request.OldThumbprintSha256) || string.IsNullOrWhiteSpace(request.NewThumbprintSha256))
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid request", detail: "Old and new thumbprints are required");
        }

        var ok = store.ConfirmCertificateRotation(deviceId, request.OldThumbprintSha256, request.NewThumbprintSha256);
        if (!ok)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid rotation state", detail: "Rotation confirmation failed");
        }

        return NoContent();
    }
}
