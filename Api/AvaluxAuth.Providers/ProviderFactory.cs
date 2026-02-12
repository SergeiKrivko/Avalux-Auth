using System.Diagnostics.CodeAnalysis;
using AvaluxAuth.Abstractions;

namespace AvaluxAuth.Providers;

public class ProviderFactory(IEnumerable<IAuthProvider> providers) : IProviderFactory
{
    public IAuthProvider GetProvider(string providerKey)
    {
        return providers.First(x => x.Key == providerKey);
    }

    public bool TryGetProvider(string providerKey, [NotNullWhen(true)] out IAuthProvider? provider)
    {
        provider = providers.FirstOrDefault(x => x.Key == providerKey);
        return provider != null;
    }

    public IAuthProvider GetProvider(int providerId)
    {
        return providers.First(x => x.Id == providerId);
    }

    public bool TryGetProvider(int providerId, [NotNullWhen(true)] out IAuthProvider? provider)
    {
        provider = providers.FirstOrDefault(x => x.Id == providerId);
        return provider != null;
    }
}