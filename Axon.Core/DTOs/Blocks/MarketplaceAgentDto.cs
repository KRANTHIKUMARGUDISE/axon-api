namespace Axon.Core.DTOs.Blocks;

public class MarketplaceAgentDto
{
    public string Name { get; set; } = default!;
    public string Path { get; set; } = default!;
    public string? Description { get; set; }
    public string? Version { get; set; }
    public DateTime? LastUpdated { get; set; }
}
