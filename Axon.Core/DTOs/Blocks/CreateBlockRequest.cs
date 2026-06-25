using Axon.Core.Enums;

namespace Axon.Core.DTOs.Blocks;

public class CreateBlockRequest
{
    public string Name { get; set; } = default!;
    public string Description { get; set; } = default!;
    public BlockRole Role { get; set; }
    public SourceType SourceType { get; set; }
    public string ArtifactName { get; set; } = default!;
    public AgentRuntime AgentRuntime { get; set; }
    public ArtifactFormat ArtifactFormat { get; set; }
    public List<string> ContextRequirements { get; set; } = [];
    public List<string> OutputSchema { get; set; } = [];
    public Dictionary<string, string>? OutputMapping { get; set; }
    public List<string> Tags { get; set; } = [];
    public List<CachedFileRequest>? CachedFiles { get; set; }
    public string? EntryPointPath { get; set; }
    public BuildingBlockVisibility? Visibility { get; set; }
    public ExecutionType? ExecutionType { get; set; }
    public List<string>? AllowedTools { get; set; }
    public int DefaultTimeoutSeconds { get; set; } = 120;
    /// <summary>Client signals agreement confirmation; server stamps the actual timestamp and acceptor identity.</summary>
    public bool ExecutionAgreementAccepted { get; set; }
}
