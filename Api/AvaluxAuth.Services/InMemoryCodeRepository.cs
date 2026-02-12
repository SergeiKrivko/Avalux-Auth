using AvaluxAuth.Abstractions;
using AvaluxAuth.Models;

namespace AvaluxAuth.Services;

public class InMemoryCodeRepository : IAuthCodeRepository
{
    private readonly Dictionary<string, AuthCode> _codes = [];

    public Task SaveCodeAsync(AuthCode code)
    {
        _codes[code.Code] = code;
        return Task.CompletedTask;
    }

    public Task<AuthCode> GetCodeAsync(string code)
    {
        return Task.FromResult(_codes[code]);
    }

    public Task DeleteCodeAsync(string code)
    {
        _codes.Remove(code);
        return Task.CompletedTask;
    }

    public Task<AuthCode> TakeCodeAsync(string code)
    {
        var result = _codes[code];
        _codes.Remove(code);
        return Task.FromResult(result);
    }
}