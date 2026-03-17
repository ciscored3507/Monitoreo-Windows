using Audit.Backend.Worker;
using Audit.Backend.Infrastructure.Persistence;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddWindowsService(options => options.ServiceName = "AuditBackendWorker");
var storageOptions = builder.Configuration.GetSection("BackendStorage").Get<BackendStorageOptions>() ?? new BackendStorageOptions();
builder.Services.AddSingleton(storageOptions);
builder.Services.AddSingleton<CertificateStateRepository>();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
