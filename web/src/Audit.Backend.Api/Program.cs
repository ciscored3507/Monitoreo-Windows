using Audit.Backend.Api.Grpc;
using Audit.Backend.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddControllers();
builder.Services.AddGrpc();
builder.Services.AddOpenApi();
builder.Services.AddSingleton<IEnrollmentStore, InMemoryEnrollmentStore>();
builder.Services.AddSingleton<IChunkStore, InMemoryChunkStore>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseExceptionHandler();
app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.MapGrpcService<IngestServiceImpl>();
app.MapGet("/", () => Results.Ok(new
{
    service = "Audit.Backend.Api",
    status = "ok"
}));

app.Run();
