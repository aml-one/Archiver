using System.Diagnostics;
using System.Security.Principal;

namespace Archiver.UI.Services;

/// <summary>
/// Controls the Archiver Windows Service via sc.exe commands.
/// Avoids dependency on System.ServiceProcess which requires the service
/// to be running to query status, and handles elevation correctly.
/// </summary>
public sealed class WindowsServiceController : IServiceController
{
    private const string ServiceName = "Archiver";

    public bool IsElevated { get; }

    public WindowsServiceController()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        IsElevated = principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    public ServiceStatusInfo GetStatus()
    {
        var info = new ServiceStatusInfo();

        try
        {
            var result = RunScCommand("query", ServiceName, out var output, out var error);

            if (result != 0)
            {
                info.IsInstalled = false;
                info.IsRunning = false;
                info.StatusText = "Not Installed";
                info.ErrorMessage = $"Service '{ServiceName}' is not installed. Run Install.ps1 as Administrator.";
                return info;
            }

            info.IsInstalled = true;

            // Parse sc query output for STATE line
            if (output.Contains("RUNNING"))
            {
                info.IsRunning = true;
                info.StatusText = "Running";
            }
            else if (output.Contains("STOPPED"))
            {
                info.IsRunning = false;
                info.StatusText = "Stopped";
            }
            else if (output.Contains("START_PENDING"))
            {
                info.IsRunning = false;
                info.StatusText = "Starting...";
            }
            else if (output.Contains("STOP_PENDING"))
            {
                info.IsRunning = true;
                info.StatusText = "Stopping...";
            }
            else
            {
                info.StatusText = "Unknown";
            }
        }
        catch (Exception ex)
        {
            info.IsInstalled = false;
            info.IsRunning = false;
            info.StatusText = "Error";
            info.ErrorMessage = ex.Message;
        }

        return info;
    }

    public async Task<bool> StartAsync()
    {
        if (!IsElevated)
            return false;

        return await Task.Run(() =>
        {
            var result = RunScCommand("start", ServiceName, out _, out _);
            return result == 0;
        });
    }

    public async Task<bool> StopAsync()
    {
        if (!IsElevated)
            return false;

        return await Task.Run(() =>
        {
            var result = RunScCommand("stop", ServiceName, out _, out _);
            return result == 0;
        });
    }

    private static int RunScCommand(string command, string serviceName, out string output, out string error)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "sc.exe",
            Arguments = $"{command} \"{serviceName}\"",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        using var process = Process.Start(psi);
        if (process == null)
        {
            output = "";
            error = "Failed to start sc.exe";
            return -1;
        }

        process.WaitForExit(10000);
        output = process.StandardOutput.ReadToEnd();
        error = process.StandardError.ReadToEnd();
        return process.ExitCode;
    }
}
