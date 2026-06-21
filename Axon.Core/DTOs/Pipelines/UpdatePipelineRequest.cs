using Axon.Core.Enums;
using Axon.Core.Models;

namespace Axon.Core.DTOs.Pipelines;

public class UpdatePipelineRequest
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public List<string>? Tags { get; set; }
    public PipelineVisibility? Visibility { get; set; }
    public List<PipelineNode>? Nodes { get; set; }
    public List<PipelineEdge>? Edges { get; set; }
}
