using System.Text.Json.Serialization;

namespace Archiver.Core.Configuration;

/// <summary>
/// Root configuration model for the Archiver application.
/// Serialized to/from C:\ProgramData\Archiver\config.json.
/// </summary>
public sealed class ArchiverOptions
{
    [JsonPropertyName("service")]
    public ServiceOptions Service { get; set; } = new();

    [JsonPropertyName("credentials")]
    public List<CredentialOptions>? Credentials { get; set; }

    [JsonPropertyName("jobs")]
    public List<CopyJobOptions> Jobs { get; set; } = new();

    /// <summary>Get a credential by name, or null if not found.</summary>
    public CredentialOptions? GetCredential(string? name)
    {
        if (string.IsNullOrWhiteSpace(name) || Credentials == null)
            return null;
        return Credentials.FirstOrDefault(c =>
            string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Find a credential that covers the given share path.</summary>
    public CredentialOptions? FindCredentialForShare(string sharePath)
    {
        if (Credentials == null || string.IsNullOrWhiteSpace(sharePath))
            return null;

        return Credentials.FirstOrDefault(c =>
            c.TargetShares?.Any(s =>
                sharePath.StartsWith(s, StringComparison.OrdinalIgnoreCase)) == true);
    }
}
