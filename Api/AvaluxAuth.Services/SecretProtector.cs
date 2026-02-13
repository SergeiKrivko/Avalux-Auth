using AvaluxAuth.Abstractions;
using Microsoft.AspNetCore.DataProtection;

namespace AvaluxAuth.Services;

public sealed class SecretProtector(IDataProtectionProvider dataProtectionProvider) : ISecretProtector
{
    private readonly IDataProtector _protector = dataProtectionProvider.CreateProtector("AuthService.Secrets");

    public string Protect(string plaintext) => _protector.Protect(plaintext);

    public string Unprotect(string protectedValue) => _protector.Unprotect(protectedValue);
}
