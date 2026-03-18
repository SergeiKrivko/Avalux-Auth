using AvaluxAuth.Models;

namespace AvaluxAuth.Abstractions;

public interface ILinkCodeRepository
{
    public Task SaveCodeAsync(LinkCode code);
    public Task<LinkCode?> GetCodeAsync(string code);
    public Task DeleteCodeAsync(string code);
    public Task<LinkCode?> TakeCodeAsync(string code);
}