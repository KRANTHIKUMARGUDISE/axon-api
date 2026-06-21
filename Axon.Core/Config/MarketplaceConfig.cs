namespace Axon.Core.Config;

public class MarketplaceConfig
{
    public string GithubRepo { get; set; } = default!;
    public string GithubToken { get; set; } = default!;
    public int CacheTtlHours { get; set; } = 6;
}
