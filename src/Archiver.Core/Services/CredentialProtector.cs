using System.Security.Cryptography;

namespace Archiver.Core.Services;

/// <summary>
/// Uses Windows DPAPI (ProtectedData) for credential encryption.
/// The data can only be decrypted by the same user account that encrypted it.
/// This means the WPF UI user and the service account must be the same for
/// decryption to work — or the config must be saved by the service account.
///
/// For the WPF UI scenario: the user saves credentials, they are encrypted
/// under that user's identity. If the service runs as LocalSystem, it cannot
/// decrypt them. In that case, configure the service to run as the same user,
/// or use the "net use" method with the service managing its own credentials.
/// </summary>
public sealed class CredentialProtector : ICredentialProtector
{
    public string Protect(string plaintext)
    {
        if (string.IsNullOrEmpty(plaintext))
            return string.Empty;

        var plainBytes = System.Text.Encoding.UTF8.GetBytes(plaintext);
        var cipherBytes = ProtectedData.Protect(plainBytes, null, DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(cipherBytes);
    }

    public string Unprotect(string ciphertextBase64)
    {
        if (string.IsNullOrEmpty(ciphertextBase64))
            return string.Empty;

        try
        {
            var cipherBytes = Convert.FromBase64String(ciphertextBase64);
            var plainBytes = ProtectedData.Unprotect(cipherBytes, null, DataProtectionScope.CurrentUser);
            return System.Text.Encoding.UTF8.GetString(plainBytes);
        }
        catch (CryptographicException)
        {
            // Cannot decrypt — likely encrypted by a different user account
            return string.Empty;
        }
    }
}
