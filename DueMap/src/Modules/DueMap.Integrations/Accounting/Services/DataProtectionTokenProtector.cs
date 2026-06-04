using Microsoft.AspNetCore.DataProtection;

namespace DueMap.Integrations.Accounting.Services;

internal sealed class DataProtectionTokenProtector : ITokenProtector
{
    /// <summary>
    /// The "purpose" string locks tokens to this concern; tokens encrypted here
    /// can't be decrypted by any other consumer of the same keyring.
    /// </summary>
    public const string Purpose = "DueMap.Integrations.AccountingTokens.v1";

    private readonly IDataProtector _protector;

    public DataProtectionTokenProtector(IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector(Purpose);
    }

    public string Protect(string plaintext)
    {
        ArgumentException.ThrowIfNullOrEmpty(plaintext);
        return _protector.Protect(plaintext);
    }

    public string Unprotect(string protectedValue)
    {
        ArgumentException.ThrowIfNullOrEmpty(protectedValue);
        return _protector.Unprotect(protectedValue);
    }
}
