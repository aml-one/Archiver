namespace Archiver.UI.Services;

/// <summary>
/// Abstracts Windows Service control operations.
/// </summary>
public interface IServiceController
{
    ServiceStatusInfo GetStatus();
    Task<bool> StartAsync();
    Task<bool> StopAsync();
    bool IsElevated { get; }
}

public sealed class ServiceStatusInfo
{
    public bool IsInstalled { get; set; }
    public bool IsRunning { get; set; }
    public string StatusText { get; set; } = "Unknown";
    public string? ErrorMessage { get; set; }
}
