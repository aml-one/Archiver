using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Diagnostics;
using Archiver.Core.Configuration;
using Microsoft.Extensions.Logging;

namespace Archiver.Core.Services;

/// <summary>
/// Authenticates to SMB network shares using WNetAddConnection2 (Win32) or 'net use' fallback.
/// </summary>
public sealed class NetworkShareAuthenticator : INetworkShareAuthenticator
{
    private readonly ILogger<NetworkShareAuthenticator> _logger;

    public NetworkShareAuthenticator(ILogger<NetworkShareAuthenticator> logger)
    {
        _logger = logger;
    }

    public bool Connect(string sharePath, CredentialOptions credential, string decryptedPassword)
    {
        if (credential.Method == CredentialMethod.NetUse)
            return ConnectViaNetUse(sharePath, credential, decryptedPassword);

        return ConnectViaWin32(sharePath, credential, decryptedPassword);
    }

    public bool Disconnect(string sharePath)
    {
        var result = WNetCancelConnection2(sharePath, 0, true);
        if (result != 0 && result != 2250) // 2250 = connection does not exist (already closed or never opened)
        {
            _logger.LogWarning("Failed to disconnect from {Share}: error code {ErrorCode}", sharePath, result);
            return false;
        }
        _logger.LogDebug("Disconnected from {Share}", sharePath);
        return true;
    }

    public bool TryConnect(string sharePath, CredentialOptions credential, string decryptedPassword, out string? errorMessage)
    {
        try
        {
            if (Connect(sharePath, credential, decryptedPassword))
            {
                // Verify the share is actually reachable
                if (Directory.Exists(sharePath))
                {
                    errorMessage = null;
                    return true;
                }
                errorMessage = "Connected but share path is not accessible. Check the path and permissions.";
                return false;
            }
            errorMessage = "Failed to establish connection. Check credentials and network availability.";
            return false;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            return false;
        }
    }

    private bool ConnectViaWin32(string sharePath, CredentialOptions credential, string decryptedPassword)
    {
        var netResource = new NETRESOURCE
        {
            dwType = RESOURCETYPE_DISK,
            lpRemoteName = sharePath,
            lpLocalName = null,
            lpProvider = null
        };

        // First, try to disconnect any existing connection to avoid 1219 (multiple connections)
        WNetCancelConnection2(sharePath, 0, true);

        var userPrincipal = credential.GetUserPrincipal();
        var result = WNetAddConnection2(ref netResource, decryptedPassword, userPrincipal, 0);

        if (result == 0)
        {
            _logger.LogInformation("Connected to {Share} as {User}", sharePath, userPrincipal);
            return true;
        }

        // 1219 = ERROR_SESSION_CREDENTIAL_CONFLICT — already connected with different credentials
        // 85 = ERROR_ALREADY_ASSIGNED — already connected
        if (result == 1219 || result == 85)
        {
            _logger.LogInformation("Already connected to {Share} (code {Code}). Reusing existing connection.", sharePath, result);
            return true;
        }

        var win32Error = new Win32Exception(result);
        _logger.LogError("Failed to connect to {Share} as {User}: {Error} (code {Code})",
            sharePath, userPrincipal, win32Error.Message, result);
        return false;
    }

    private bool ConnectViaNetUse(string sharePath, CredentialOptions credential, string decryptedPassword)
    {
        try
        {
            var userPrincipal = credential.GetUserPrincipal();
            var arguments = $"use \"{sharePath}\" /user:{userPrincipal} {decryptedPassword} /persistent:no";

            var psi = new ProcessStartInfo
            {
                FileName = "net",
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var process = Process.Start(psi);
            if (process == null)
            {
                _logger.LogError("Failed to start 'net use' process for {Share}", sharePath);
                return false;
            }

            process.WaitForExit(TimeSpan.FromSeconds(15));

            var stdout = process.StandardOutput.ReadToEnd();
            var stderr = process.StandardError.ReadToEnd();

            if (process.ExitCode == 0)
            {
                _logger.LogInformation("Connected to {Share} via net use as {User}", sharePath, userPrincipal);
                return true;
            }

            _logger.LogError("net use failed for {Share}: exit code {ExitCode}, stderr: {Error}",
                sharePath, process.ExitCode, stderr);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "net use command failed for {Share}", sharePath);
            return false;
        }
    }

    // ─── P/Invoke for WNetAddConnection2 ────────────────────────────────

    private const int RESOURCETYPE_DISK = 0x0001;

    [StructLayout(LayoutKind.Sequential)]
    private struct NETRESOURCE
    {
        public int dwScope;
        public int dwType;
        public int dwDisplayType;
        public int dwUsage;
        public string? lpLocalName;
        public string? lpRemoteName;
        public string? lpComment;
        public string? lpProvider;
    }

    [DllImport("mpr.dll", CharSet = CharSet.Auto)]
    private static extern int WNetAddConnection2(
        ref NETRESOURCE lpNetResource,
        string? lpPassword,
        string? lpUsername,
        int dwFlags);

    [DllImport("mpr.dll", CharSet = CharSet.Auto)]
    private static extern int WNetCancelConnection2(
        string lpName,
        int dwFlags,
        [MarshalAs(UnmanagedType.Bool)] bool fForce);
}
