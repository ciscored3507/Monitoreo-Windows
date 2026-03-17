using Audit.Backend.Worker;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddWindowsService(options => options.ServiceName = "AuditBackendWorker");
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
