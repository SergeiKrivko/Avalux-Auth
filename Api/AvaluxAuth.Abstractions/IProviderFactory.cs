using System.Diagnostics.CodeAnalysis;

namespace AvaluxAuth.Abstractions;

public interface IProviderFactory
{
    public IAuthProvider GetProvider(string providerKey);

    public bool TryGetProvider(string providerKey, [NotNullWhen(true)] out IAuthProvider? provider);
    public IAuthProvider GetProvider(int providerId);

    public bool TryGetProvider(int providerId, [NotNullWhen(true)] out IAuthProvider? provider);
}