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
    public Dictionary<string, string>? OutputMapping { get; set; }
    public List<string>? Tags { get; set; }
    public List<CachedFileRequest>? CachedFiles { get; set; }
    public string? EntryPointPath { get; set; }
    public BuildingBlockVisibility? Visibility { get; set; }
    public ExecutionType? ExecutionType { get; set; }
    public List<string>? AllowedTools { get; set; }
    public int? DefaultTimeoutSeconds { get; set; }
    /// <summary>Client signals agreement confirmation; server stamps the actual timestamp and acceptor identity.</summary>
    public bool? ExecutionAgreementAccepted { get; set; }
}
