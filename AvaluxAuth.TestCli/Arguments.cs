using CommandLine;

namespace AvaluxAuth.TestCli;

public class DefaultArguments
{
    [Option(longName: "apiUrl", Required = false)]
    public string ApiUrl { get; set; } = "https://localhost:5000";

    [Option(longName: "clientId", Required = true)]
    public required string ClientId { get; set; }

    [Option(longName: "clientSecret", Required = true)]
    public required string ClientSecret { get; set; }
}

[Verb("login")]
public class LogInArguments : DefaultArguments
{
    [Option(shortName: 'p', longName: "provider", Required = true,
        HelpText = "Ключ провайдера для авторизации")]
    public required string Provider { get; set; }
}

[Verb("link")]
public class LinkArguments : DefaultArguments
{
    [Option(shortName: 'p', longName: "provider", Required = true,
        HelpText = "Ключ провайдера для авторизации")]
    public required string Provider { get; set; }
}

[Verb("logout")]
public class LogoutArguments
{
    [Option(longName: "apiUrl", Required = false)]
    public string ApiUrl { get; set; } = "https://localhost:5000";
}