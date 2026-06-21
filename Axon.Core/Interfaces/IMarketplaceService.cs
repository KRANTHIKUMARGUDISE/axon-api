using Axon.Core.DTOs.Blocks;

namespace Axon.Core.Interfaces;

public interface IMarketplaceService
{
    Task<string?> FetchBlockDefinitionAsync(string marketplacePath);
    Task<List<MarketplaceAgentDto>> BrowseAsync();
}
