using AvaluxAuth.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace AvaluxAuth.Providers.Extensions;

public static class ServiceCollectionExtension
{
    extension(IServiceCollection serviceCollection)
    {
        public IServiceCollection AddAuthProviders()
        {
            serviceCollection.AddScoped<IProviderFactory, ProviderFactory>();

            serviceCollection.AddScoped<IAuthProvider, YandexAuthProvider>();

            serviceCollection.AddHttpClient("yandex");

            return serviceCollection;
        }
    }
}