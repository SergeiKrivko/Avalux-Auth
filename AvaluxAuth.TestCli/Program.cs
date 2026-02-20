// See https://aka.ms/new-console-template for more information

using System.Reflection;
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
            var apiClient = new ApiClient();
            apiClient.StartAuthorization(o.Provider, o.ClientId);
            var code = await Utils.ReceiveAuthCodeAsync();
            if (string.IsNullOrEmpty(code))
                throw new Exception("Code is empty");
            var credentials = await apiClient.GetAccessToken(code, o.ClientId, o.ClientSecret);

            var userInfo = await apiClient.GetUserInfo(credentials);
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
            var apiClient = new ApiClient();
            apiClient.StartAuthorization(o.Provider, o.ClientId);
            var code = await Utils.ReceiveAuthCodeAsync();
            if (string.IsNullOrEmpty(code))
                throw new Exception("Code is empty");
            var credentials = await Utils.LoadCredentials();
            if (credentials == null)
                throw new Exception("Can not load credentials");
            credentials = await apiClient.RefreshToken(credentials);
            await apiClient.LinkAccount(code, o.ClientId, o.ClientSecret, credentials);

            var userInfo = await apiClient.GetUserInfo(credentials);
            Console.WriteLine($"User ID: {userInfo.Id}");
            foreach (var account in userInfo.Accounts)
            {
                Console.WriteLine($"{account.Provider} --- {account.Name}, {account.Email}");
            }

            await Utils.SaveCredentials(credentials);
        }
            break;
        case LogoutArguments o:
            break;
    }
}