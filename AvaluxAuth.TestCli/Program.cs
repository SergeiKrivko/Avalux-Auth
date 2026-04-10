// See https://aka.ms/new-console-template for more information

using System.Reflection;
using Avalux.Auth.UserClient;
using AvaluxAuth.TestCli;
using CommandLine;

await Parser.Default.ParseArguments(args, Assembly.GetExecutingAssembly().GetTypes()
        .Where(t => t.GetCustomAttribute<VerbAttribute>() != null).ToArray())
    .WithParsedAsync(Run);

async Task Run(object obj)
{
    switch (obj)
    {
        case LogInArguments o:
        {
            var apiClient = new AuthClient(o.ApiUrl, o.ClientId, o.ClientSecret, new CredentialsStore());
            await apiClient.AuthorizeInstalledAsync(o.Provider, Utils.CallbackUrl);

            var userInfo = await apiClient.GetUserInfoAsync();
            Console.WriteLine($"User ID: {userInfo.Id}");
            foreach (var account in userInfo.Accounts)
            {
                Console.WriteLine($"{account.Provider} --- {account.Name}, {account.Email}");
            }
        }
            break;
        case LinkArguments o:
        {
            var apiClient = new AuthClient(o.ApiUrl, o.ClientId, o.ClientSecret, new CredentialsStore());
            if (apiClient.Credentials == null)
                throw new Exception("Can not load credentials");
            await apiClient.LinkInstalledAsync(o.Provider, Utils.CallbackUrl);

            var userInfo = await apiClient.GetUserInfoAsync();
            Console.WriteLine($"User ID: {userInfo.Id}");
            foreach (var account in userInfo.Accounts)
            {
                Console.WriteLine($"{account.Provider} --- {account.Name}, {account.Email}");
            }
        }
            break;
        case LogoutArguments o:
            break;
    }
}