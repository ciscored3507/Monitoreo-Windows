using Audit.Agent.Storage;
using Audit.Agent.Transport;
using Audit.Agent.Contracts;

namespace Audit.Agent.Service;

public class Worker(
    ILogger<Worker> logger,
    IChunkQueue chunkQueue,
    IIngestClient ingestClient,
    IAgentControlClient controlClient,
    AgentTransportOptions options) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Audit Agent Service iniciado");
        DateTimeOffset lastControlSync = DateTimeOffset.MinValue;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (DateTimeOffset.UtcNow - lastControlSync > TimeSpan.FromSeconds(30))
                {
                    var depthNow = await chunkQueue.GetDepthAsync(stoppingToken);
                    var heartbeat = new Audit.Agent.Contracts.HeartbeatDto(
                        DateTimeOffset.UtcNow,
                        "1.0.0-dev",
                        depthNow,
                        null,
                        ["ok"]);
                    var hbResult = await controlClient.PostHeartbeatAsync(options.DeviceId, heartbeat, stoppingToken);
                    var policy = await controlClient.GetPolicyAsync(options.DeviceId, stoppingToken);
                    logger.LogInformation(
                        "Control sync OK. ServerTime={ServerTime} PolicyVersion={PolicyVersion} ChunkSeconds={ChunkSeconds}",
                        hbResult.ServerTimeUtc,
                        policy.Version,
                        policy.Capture.ChunkSeconds);

                    if (hbResult.CertificateRotation is not null)
                    {
                        await HandleCertificateRotationAsync(hbResult.CertificateRotation, controlClient, options, stoppingToken);
                    }

                    lastControlSync = DateTimeOffset.UtcNow;
                }

                var batch = await chunkQueue.LeaseBatchAsync(10, TimeSpan.FromMinutes(2), stoppingToken);
                if (batch.Count == 0)
                {
                    logger.LogInformation("Heartbeat del servicio a las {time}. Cola vacia.", DateTimeOffset.Now);
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                    continue;
                }

                foreach (var chunk in batch)
                {
                    try
                    {
                        var result = await ingestClient.UploadChunkAsync(chunk, stoppingToken);
                        await chunkQueue.MarkUploadedAsync(chunk.ChunkId, stoppingToken);
                        logger.LogInformation("Chunk {ChunkId} subido. Stored={Stored} Key={ObjectKey}", result.ChunkId, result.Stored, result.ObjectKey);
                    }
                    catch (InvalidOperationException ex) when (ex.Message == "hash_mismatch")
                    {
                        await chunkQueue.MoveToDeadLetterAsync(chunk.ChunkId, ex.Message, stoppingToken);
                        logger.LogWarning(ex, "Chunk {ChunkId} movido a dead-letter por hash mismatch", chunk.ChunkId);
                    }
                    catch (Exception ex)
                    {
                        var backoff = Math.Min((int)Math.Pow(2, Math.Max(1, chunk.Attempts + 1)), 300);
                        var nextRetry = DateTimeOffset.UtcNow.AddSeconds(backoff);
                        await chunkQueue.MarkFailedAsync(chunk.ChunkId, ex.Message, nextRetry, stoppingToken);
                        logger.LogWarning(ex, "Fallo de subida chunk {ChunkId}. Reintento en {NextRetry}", chunk.ChunkId, nextRetry);
                    }
                }
            }
            catch (HttpRequestException ex)
            {
                logger.LogWarning(ex, "Fallo de conectividad con backend de control.");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async Task HandleCertificateRotationAsync(
        CertificateRotationInstructionDto instruction,
        IAgentControlClient controlClient,
        AgentTransportOptions options,
        CancellationToken ct)
    {
        var currentThumb = Normalize(options.ClientCertificateThumbprint);
        var previousThumb = Normalize(options.PreviousCertificateThumbprint);
        var targetThumb = Normalize(instruction.NewThumbprintSha256);

        if (string.IsNullOrWhiteSpace(targetThumb))
        {
            return;
        }

        if (!string.Equals(currentThumb, targetThumb, StringComparison.Ordinal))
        {
            if (string.IsNullOrWhiteSpace(currentThumb))
            {
                logger.LogWarning("No hay thumbprint actual configurado para iniciar rotacion.");
                return;
            }

            var start = await controlClient.StartCertificateRotationAsync(
                new StartRotationRequest(targetThumb, instruction.GraceSeconds),
                ct);

            logger.LogWarning(
                "Rotacion iniciada Old={OldThumb} New={NewThumb} RevokeAfter={RevokeAfterUtc}",
                start.OldThumbprintSha256,
                start.NewThumbprintSha256,
                start.RevokeAfterUtc);

            if (options.SimulateInProcessRotation)
            {
                options.PreviousCertificateThumbprint = currentThumb;
                options.ClientCertificateThumbprint = targetThumb;
                logger.LogWarning("Modo simulacion activo: thumbprint local actualizado en memoria.");
            }

            return;
        }

        if (string.IsNullOrWhiteSpace(previousThumb))
        {
            return;
        }

        await controlClient.ConfirmCertificateRotationAsync(new ConfirmRotationRequest(previousThumb, currentThumb), ct);
        logger.LogInformation("Rotacion confirmada. Certificado previo revocado en backend.");
        options.PreviousCertificateThumbprint = null;
    }

    private static string Normalize(string? value)
    {
        return (value ?? string.Empty).Replace(" ", string.Empty, StringComparison.Ordinal).ToUpperInvariant();
    }
}
