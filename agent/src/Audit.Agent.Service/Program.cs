using Audit.Agent.Service;
using Audit.Agent.Storage;
using Audit.Agent.Transport;
using Grpc.Net.Client;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddWindowsService(options => options.ServiceName = "AuditAgentService");
var transportOptions = builder.Configuration.GetSection("Backend").Get<AgentTransportOptions>() ?? new AgentTransportOptions();
builder.Services.AddSingleton(transportOptions);

builder.Services.AddSingleton<IChunkQueue>(_ =>
{
    var path = builder.Configuration["Queue:DatabasePath"];
    if (string.IsNullOrWhiteSpace(path))
    {
        path = Path.Combine("data", "agent-spool.db");
    }

    return new SqliteChunkQueue(path);
});

builder.Services.AddHttpClient<IAgentControlClient, RestAgentControlClient>((_, client) =>
{
    client.BaseAddress = transportOptions.RestBaseUrl;
})
.ConfigurePrimaryHttpMessageHandler(() => TransportSecurity.CreateHttpHandler(transportOptions));

builder.Services.AddSingleton<IIngestClient>(sp =>
{
    if (transportOptions.UseSimulatedUpload)
    {
        return new SimulatedIngestClient();
    }

    var handler = TransportSecurity.CreateHttpHandler(transportOptions);

    var channel = GrpcChannel.ForAddress(transportOptions.GrpcEndpoint, new GrpcChannelOptions
    {
        HttpHandler = handler
    });

    return new GrpcIngestClient(channel, transportOptions);
});
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
