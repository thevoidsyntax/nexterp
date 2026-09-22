namespace ERP.Application.Common.Interfaces;

/// <summary>
/// Encrypts/decrypts sensitive field values at rest (e.g. employee banking details).
/// </summary>
public interface IEncryptionService
{
    string Encrypt(string plaintext);

    /// <summary>
    /// Decrypts a value produced by <see cref="Encrypt"/>. If the input isn't in the
    /// expected protected format (e.g. a value written before encryption was enabled),
    /// it is returned unchanged instead of throwing, so old plaintext rows stay readable.
    /// </summary>
    string Decrypt(string value);
}
