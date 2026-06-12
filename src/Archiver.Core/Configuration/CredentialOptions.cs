using System.Text.Json.Serialization;

namespace Archiver.Core.Configuration;

public enum CredentialMethod
{
    /// <summary>Use Win32 WNetAddConnection2 API (default).</summary>
    Win32,
    /// <summary>Shell out to 'net use' command.</summary>
    NetUse
}

/// <summary>
/// Stored credential for accessing a password-protected network share.
/// The password is encrypted via DPAPI; only the encrypting account can decrypt.
/// </summary>
public sealed class CredentialOptions
{
    /// <summary>Logical name for this credential, referenced by jobs via <see cref="CopyJobOptions.CredentialName"/>.</summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>Windows domain (optional — omit for workgroup).</summary>
    [JsonPropertyName("domain")]
    public string? Domain { get; set; }

    /// <summary>Username for the share.</summary>
    [JsonPropertyName("username")]
    public string Username { get; set; } = string.Empty;

    /// <summary>DPAPI-encrypted password (Base64). Written by the WPF UI, read by the service.</summary>
    [JsonPropertyName("passwordEncrypted")]
    public string? PasswordEncrypted { get; set; }

    /// <summary>
    /// List of UNC share paths this credential applies to (e.g. "\\NAS\Backups").
    /// Used to auto-match credentials to jobs when not explicitly referenced.
    /// </summary>
    [JsonPropertyName("targetShares")]
    public List<string>? TargetShares { get; set; }

    /// <summary>Authentication method to use for this credential.</summary>
    [JsonPropertyName("method")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public CredentialMethod Method { get; set; } = CredentialMethod.Win32;

    /// <summary>Build the user principal string for WNetAddConnection2 (DOMAIN\user or user).</summary>
    public string GetUserPrincipal()
    {
        return string.IsNullOrEmpty(Domain) ? Username : $"{Domain}\\{Username}";
    }
}
