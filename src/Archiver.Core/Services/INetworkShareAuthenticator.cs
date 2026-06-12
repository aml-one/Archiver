using Archiver.Core.Configuration;

namespace Archiver.Core.Services;

/// <summary>
/// Authenticates to network shares using stored credentials.
/// </summary>
public interface INetworkShareAuthenticator
{
    /// <summary>
    /// Establish a connection to the network share using the given credential.
    /// Returns true if connection succeeded (or already exists).
    /// </summary>
    bool Connect(string sharePath, CredentialOptions credential, string decryptedPassword);

    /// <summary>
    /// Disconnect from a network share.
    /// </summary>
    bool Disconnect(string sharePath);

    /// <summary>
    /// Test a connection without performing any operations.
    /// </summary>
    bool TryConnect(string sharePath, CredentialOptions credential, string decryptedPassword, out string? errorMessage);
}
