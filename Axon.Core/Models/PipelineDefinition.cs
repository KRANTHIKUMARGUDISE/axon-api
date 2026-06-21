using Axon.Core.Enums;

namespace Axon.Core.Models;

public class PipelineDefinition
{
    public string Id { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string Description { get; set; } = default!;
    public List<string> Tags { get; set; } = [];
    public int Version { get; set; }
    public string VersionLabel { get; set; } = default!;
    public List<PipelineNode> Nodes { get; set; } = [];
    public List<PipelineEdge> Edges { get; set; } = [];
    public PipelineVisibility Visibility { get; set; }
    public string CreatedBy { get; set; } = default!;
    public string CreatedByName { get; set; } = default!;
    public string? TeamId { get; set; }
    public string OwnerTeam { get; set; } = default!;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
