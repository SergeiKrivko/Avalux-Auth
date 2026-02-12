using AvaluxAuth.Models;

namespace AvaluxAuth.Api.Schemas;

public class CreateProviderSchema
{
    public required int ProviderId { get; init; }
    public required ProviderParameters Parameters { get; init; }
}