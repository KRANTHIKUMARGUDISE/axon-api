using Axon.Core.Models;

namespace Axon.Core.DTOs.Pipelines;

public class ValidateRequest
{
    public List<PipelineNode> Nodes { get; set; } = [];
    public List<PipelineEdge> Edges { get; set; } = [];
}
