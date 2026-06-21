namespace Axon.Core.Models;

public class EdgeCondition
{
    public string Field { get; set; } = default!;
    public string Operator { get; set; } = default!;
    public string Value { get; set; } = default!;
}

public class PipelineEdge
{
    public string Id { get; set; } = default!;
    public string SourceNodeId { get; set; } = default!;
    public string TargetNodeId { get; set; } = default!;
    public bool IsDefault { get; set; }
    public EdgeCondition? Condition { get; set; }
}
