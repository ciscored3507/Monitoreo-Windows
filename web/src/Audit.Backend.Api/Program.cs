using Audit.Backend.Api.Grpc;
using Audit.Backend.Api.Services;
using Audit.Backend.Infrastructure.Persistence;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Certificate;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddControllers();
builder.Services.AddGrpc();
builder.Services.AddOpenApi();
var storageOptions = builder.Configuration.GetSection("BackendStorage").Get<BackendStorageOptions>() ?? new BackendStorageOptions();
builder.Services.AddSingleton(storageOptions);
builder.Services.AddSingleton<CertificateStateRepository>();
builder.Services.AddSingleton<IEnrollmentStore, InMemoryEnrollmentStore>();
builder.Services.AddSingleton<IChunkStore, InMemoryChunkStore>();
builder.Services
    .AddAuthentication(CertificateAuthenticationDefaults.AuthenticationScheme)
    .AddCertificate(options =>
    {
        options.AllowedCertificateTypes = CertificateTypes.All;
        options.Events = new CertificateAuthenticationEvents
        {
            OnCertificateValidated = context =>
            {
                var enrollmentStore = context.HttpContext.RequestServices.GetRequiredService<IEnrollmentStore>();
                var thumbprint = context.ClientCertificate?.Thumbprint;
                if (string.IsNullOrWhiteSpace(thumbprint) ||
                    !enrollmentStore.TryResolveByCertificateThumbprint(thumbprint, out var deviceId, out var tenantId))
                {
                    context.Fail("unknown_client_certificate");
                    return Task.CompletedTask;
                }

                var claims = new[]
                {
                    new Claim("device_id", deviceId.ToString()),
                    new Claim("tenant_id", tenantId.ToString()),
                    new Claim("cert_thumbprint", thumbprint)
                };
                context.Principal = new ClaimsPrincipal(new ClaimsIdentity(claims, context.Scheme.Name));
                context.Success();
                return Task.CompletedTask;
            }
        };
    });
builder.Services.AddAuthorization();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseExceptionHandler();
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapGrpcService<IngestServiceImpl>();
app.MapGet("/", () => Results.Ok(new
{
    service = "Audit.Backend.Api",
    status = "ok"
}));

app.Run();
