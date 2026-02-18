using AvaluxAuth.Models;

namespace AvaluxAuth.Abstractions;

public interface IAuthCodeRepository
{
    public Task SaveCodeAsync(AuthCode code);
    public Task<AuthCode?> GetCodeAsync(string code);
    public Task DeleteCodeAsync(string code);
    public Task<AuthCode?> TakeCodeAsync(string code);
}