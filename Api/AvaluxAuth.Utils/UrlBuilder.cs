using System.Text;

namespace AvaluxAuth.Utils;

public class UrlBuilder
{
    private readonly string _baseUrl;
    private readonly Dictionary<string, string> _queryParams = [];

    public UrlBuilder(string baseUrl)
    {
        _baseUrl = baseUrl;
    }

    public UrlBuilder(string scheme, string host, int port)
    {
        _baseUrl = $"{scheme}://{host}:{port}";
    }

    public UrlBuilder AddQuery(string key, string value)
    {
        _queryParams.Add(key, value);
        return this;
    }

    public override string ToString()
    {
        var stringBuilder = new StringBuilder();
        stringBuilder.Append(_baseUrl);
        var i = 0;
        foreach (var (key, value) in _queryParams)
        {
            stringBuilder.Append(i == 0 ? '?' : '&')
                .Append(key)
                .Append('=')
                .Append(Uri.EscapeDataString(value));
            i++;
        }
        return stringBuilder.ToString();
    }

    public Uri ToUri()
    {
        return new Uri(ToString());
    }
}