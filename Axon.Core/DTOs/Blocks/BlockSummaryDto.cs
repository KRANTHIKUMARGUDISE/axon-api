using Axon.Core.Enums;

namespace Axon.Core.DTOs.Blocks;

public class BlockSummaryDto
{
    public string Id { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string Description { get; set; } = default!;
    public BlockRole Role { get; set; }
    public SourceType SourceType { get; set; }
    public string ArtifactName { get; set; } = default!;
    public AgentRuntime AgentRuntime { get; set; }
    public ArtifactFormat ArtifactFormat { get; set; }
    public List<string> ContextRequirements { get; set; } = [];
    public List<string> OutputSchema { get; set; } = [];
    public List<string> Tags { get; set; } = [];
    public int RunCount { get; set; }
    public bool IsActive { get; set; }
    public string CreatedBy { get; set; } = default!;
    public string CreatedByName { get; set; } = default!;
    public BuildingBlockVisibility? Visibility { get; set; }
    public ExecutionType? ExecutionType { get; set; }
    public int DefaultTimeoutSeconds { get; set; }
}
