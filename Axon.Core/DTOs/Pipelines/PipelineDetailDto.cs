using Axon.Core.Enums;
using Axon.Core.Models;

namespace Axon.Core.DTOs.Pipelines;

public class PipelineDetailDto
{
    public string Id { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public List<string> Tags { get; set; } = [];
    public int Version { get; set; }
    public string? VersionLabel { get; set; }
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
