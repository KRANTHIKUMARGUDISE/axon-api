using Axon.Core.Enums;
using Axon.Core.Models;

namespace Axon.Core.DTOs.Blocks;

public class BlockDetailDto
{
    public string Id { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string Description { get; set; } = default!;
    public BlockRole Role { get; set; }
    public List<string> ContextRequirements { get; set; } = [];
    public List<string> OutputSchema { get; set; } = [];
    public List<string> Tags { get; set; } = [];
    public int RunCount { get; set; }
    public string CreatedBy { get; set; } = default!;
    public string CreatedByName { get; set; } = default!;
    public SourceType SourceType { get; set; }
    public string ArtifactName { get; set; } = default!;
    public int Version { get; set; }
    public AgentRuntime AgentRuntime { get; set; }
    public ArtifactFormat ArtifactFormat { get; set; }
    public List<CachedFile>? CachedFiles { get; set; }
    public string? EntryPointPath { get; set; }
    public string? MarketplaceSource { get; set; }
    public string? MarketplacePath { get; set; }
    public string? MarketplaceVersion { get; set; }
    public SyncStatus SyncStatus { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
