namespace DueMap.Integrations.Accounting;

/// <summary>
/// Thin abstraction over ASP.NET Core Data Protection for OAuth tokens.
/// Keeps the rest of the codebase from depending directly on
/// <c>IDataProtectionProvider</c> and gives a single seam for swapping in
/// a different provider (Key Vault, KMS) later.
/// </summary>
public interface ITokenProtector
{
    string Protect(string plaintext);

    string Unprotect(string protectedValue);
}
