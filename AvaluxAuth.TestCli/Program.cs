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
            var apiClient = new AuthClient(o.ApiUrl, o.ClientId, o.ClientSecret);
            var credentials = await apiClient.AuthorizeInstalledAsync(o.Provider, Utils.CallbackUrl);

            var userInfo = await apiClient.GetUserInfoAsync();
            Console.WriteLine($"User ID: {userInfo.Id}");
            foreach (var account in userInfo.Accounts)
            {
                Console.WriteLine($"{account.Provider} --- {account.Name}, {account.Email}");
            }

            await Utils.SaveCredentials(credentials);
        }
            break;
        case LinkArguments o:
        {
            var apiClient = new AuthClient(o.ApiUrl, o.ClientId, o.ClientSecret)
            {
                Credentials = await Utils.LoadCredentials()
            };
            if (apiClient.Credentials == null)
                throw new Exception("Can not load credentials");
            try
            {
                await apiClient.LinkInstalledAsync(o.Provider, Utils.CallbackUrl);

                var userInfo = await apiClient.GetUserInfoAsync();
                Console.WriteLine($"User ID: {userInfo.Id}");
                foreach (var account in userInfo.Accounts)
                {
                    Console.WriteLine($"{account.Provider} --- {account.Name}, {account.Email}");
                }
            }
            catch (Exception)
            {
                if (apiClient.Credentials != null)
                    await Utils.SaveCredentials(apiClient.Credentials);
                throw;
            }

            await Utils.SaveCredentials(apiClient.Credentials);
        }
            break;
        case LogoutArguments o:
            break;
    }
}