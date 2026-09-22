using System.Security.Cryptography;
using ERP.Application.Common.Interfaces;
using Microsoft.AspNetCore.DataProtection;

namespace ERP.Infrastructure.Services;

/// <summary>
/// Encrypts sensitive field values at rest using ASP.NET Core Data Protection.
/// Keys must be persisted somewhere shared/durable (e.g. Redis) so encrypted values
/// stay decryptable across restarts and multiple instances — see Program.cs.
/// </summary>
public class DataProtectionEncryptionService : IEncryptionService
{
    private const string Purpose = "ERP.Infrastructure.PII.v1";
    private readonly IDataProtector _protector;

    public DataProtectionEncryptionService(IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector(Purpose);
    }

    public string Encrypt(string plaintext) => _protector.Protect(plaintext);

    public string Decrypt(string value)
    {
        try
        {
            return _protector.Unprotect(value);
        }
        catch (CryptographicException)
        {
            // Not in protected format — a value written before encryption was enabled.
            // Return as-is rather than failing the read; it will be re-encrypted on next save.
            return value;
        }
    }
}
