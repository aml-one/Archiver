namespace Archiver.Core.Services;

/// <summary>
/// Protects and unprotects sensitive data using Windows DPAPI.
/// </summary>
public interface ICredentialProtector
{
    /// <summary>Encrypt plaintext and return Base64-encoded ciphertext.</summary>
    string Protect(string plaintext);

    /// <summary>Decrypt Base64-encoded ciphertext back to plaintext.</summary>
    string Unprotect(string ciphertextBase64);
}
