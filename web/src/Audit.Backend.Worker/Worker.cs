using Audit.Backend.Infrastructure.Persistence;

namespace Audit.Backend.Worker;

public class Worker(ILogger<Worker> logger, CertificateStateRepository repository) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Backend Worker inicializado");
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var due = repository.GetDueConfirmedRotations(DateTimeOffset.UtcNow);
                foreach (var rotation in due)
                {
                    var revoked = repository.RevokeCertificate(rotation.DeviceId, rotation.OldThumbprintSha256, "rotation_grace_elapsed");
                    if (revoked)
                    {
                        repository.UpsertDevice(rotation.DeviceId, $"device-{rotation.DeviceId:N}", "active", DateTimeOffset.UtcNow);
                        logger.LogInformation(
                            "Certificado previo revocado por grace window. Device={DeviceId} Old={OldThumb} Due={DueAt}",
                            rotation.DeviceId,
                            rotation.OldThumbprintSha256,
                            rotation.RevokeAfterUtc);
                    }
                    else
                    {
                        logger.LogWarning(
                            "No se pudo revocar certificado previo. Device={DeviceId} Old={OldThumb}",
                            rotation.DeviceId,
                            rotation.OldThumbprintSha256);
                    }

                    repository.MarkRotationProcessed(rotation.DeviceId);
                }

                logger.LogInformation("Ciclo de mantenimiento ejecutado a las {time}. Rotaciones procesadas={Count}", DateTimeOffset.Now, due.Count);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error en ciclo de revocacion diferida");
            }

            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }
}
