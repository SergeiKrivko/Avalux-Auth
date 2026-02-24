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
            serviceCollection.AddScoped<IAuthProvider, GitHubAuthProvider>();
            serviceCollection.AddScoped<IAuthProvider, GitLabAuthProvider>();
            serviceCollection.AddScoped<IAuthProvider, MicrosoftAuthProvider>();

            serviceCollection.AddHttpClient("yandex");
            serviceCollection.AddHttpClient("google");
            serviceCollection.AddHttpClient("github");
            serviceCollection.AddHttpClient("gitlab");
            serviceCollection.AddHttpClient("microsoft");

            return serviceCollection;
        }
    }
}