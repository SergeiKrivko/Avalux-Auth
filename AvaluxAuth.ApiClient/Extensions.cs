using System.Net.Http.Headers;
using Microsoft.Extensions.DependencyInjection;

namespace Avalux.Auth.ApiClient;

public static class Extensions
{
    public static IServiceCollection AddAvaluxAuthApiClient(this IServiceCollection services, string apiUrl,
        string apiKey)
    {
        services.AddHttpClient("AvaluxAuth", options =>
        {
            options.BaseAddress = new Uri(apiUrl);
            options.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        });

        services.AddScoped<IAuthClient, HttpClientFactoryAuthClient>();

        return services;
    }

    private class HttpClientFactoryAuthClient(IHttpClientFactory httpClientFactory)
        : AuthClient(httpClientFactory.CreateClient("AvaluxAuth"));
}