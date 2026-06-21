using Axon.Core.Enums;
using Axon.Core.Models;

namespace Axon.Core.DTOs.Pipelines;

public class PipelineSummaryDto
{
    public string Id { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public List<string> Tags { get; set; } = [];
    public int Version { get; set; }
    public List<PipelineNode> Nodes { get; set; } = [];
    public List<PipelineEdge> Edges { get; set; } = [];
    public string? VersionLabel { get; set; }
    public PipelineVisibility Visibility { get; set; }
    public int NodeCount { get; set; }
    public string OwnerTeam { get; set; } = default!;
    public string CreatedBy { get; set; } = default!;
    public string CreatedByName { get; set; } = default!;
    public DateTime CreatedAt { get; set; }
}
