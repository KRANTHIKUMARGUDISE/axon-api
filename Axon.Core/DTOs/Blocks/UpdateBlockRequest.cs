using Axon.Core.Enums;

namespace Axon.Core.DTOs.Blocks;

public class UpdateBlockRequest
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public BlockRole? Role { get; set; }
    public string? ArtifactName { get; set; }
    public AgentRuntime? AgentRuntime { get; set; }
    public ArtifactFormat? ArtifactFormat { get; set; }
    public List<string>? ContextRequirements { get; set; }
    public List<string>? OutputSchema { get; set; }
    public List<string>? Tags { get; set; }
    public List<CachedFileRequest>? CachedFiles { get; set; }
    public string? EntryPointPath { get; set; }
}
