namespace Audit.Backend.Infrastructure.Persistence;

public sealed class BackendStorageOptions
{
    public string DatabasePath { get; set; } = "data\\backend-state.db";
}
