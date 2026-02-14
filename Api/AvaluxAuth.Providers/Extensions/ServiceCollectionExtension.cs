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
            serviceCollection.AddScoped<IAuthProvider, GoogleAuthProvider>();

            serviceCollection.AddHttpClient("yandex");
            serviceCollection.AddHttpClient("google");

            return serviceCollection;
        }
    }
}