using Audit.Agent.Service;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddWindowsService(options => options.ServiceName = "AuditAgentService");
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
