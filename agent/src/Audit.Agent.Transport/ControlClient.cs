using System.Net.Http.Json;
using Audit.Agent.Contracts;

namespace Audit.Agent.Transport;

public interface IAgentControlClient
{
    Task<PolicyDto> GetPolicyAsync(Guid deviceId, CancellationToken ct);
    Task<HeartbeatResponse> PostHeartbeatAsync(Guid deviceId, HeartbeatDto heartbeat, CancellationToken ct);
    Task<StartRotationResponse> StartCertificateRotationAsync(StartRotationRequest request, CancellationToken ct);
    Task ConfirmCertificateRotationAsync(ConfirmRotationRequest request, CancellationToken ct);
}

public sealed class RestAgentControlClient(HttpClient httpClient) : IAgentControlClient
{
    public async Task<PolicyDto> GetPolicyAsync(Guid deviceId, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "api/v1/agent/policy");
        request.Headers.Add("X-Device-Id", deviceId.ToString());

        using var response = await httpClient.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<PolicyDto>(cancellationToken: ct);
        return payload ?? throw new InvalidOperationException("policy_response_empty");
    }

    public async Task<HeartbeatResponse> PostHeartbeatAsync(Guid deviceId, HeartbeatDto heartbeat, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "api/v1/agent/heartbeat");
        request.Headers.Add("X-Device-Id", deviceId.ToString());
        request.Content = JsonContent.Create(heartbeat);

        using var response = await httpClient.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<HeartbeatResponse>(cancellationToken: ct);
        return payload ?? throw new InvalidOperationException("heartbeat_response_empty");
    }

    public async Task<StartRotationResponse> StartCertificateRotationAsync(StartRotationRequest request, CancellationToken ct)
    {
        using var response = await httpClient.PostAsJsonAsync("api/v1/agent/certificate/rotation/start", request, ct);
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<StartRotationResponse>(cancellationToken: ct);
        return payload ?? throw new InvalidOperationException("rotation_start_response_empty");
    }

    public async Task ConfirmCertificateRotationAsync(ConfirmRotationRequest request, CancellationToken ct)
    {
        using var response = await httpClient.PostAsJsonAsync("api/v1/agent/certificate/rotation/confirm", request, ct);
        response.EnsureSuccessStatusCode();
    }
}
