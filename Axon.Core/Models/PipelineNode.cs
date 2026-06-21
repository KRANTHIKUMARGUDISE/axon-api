namespace Axon.Core.Models;

public class NodePosition
{
    public double X { get; set; }
    public double Y { get; set; }
}

public class PipelineNode
{
    public string Id { get; set; } = default!;
    public string BlockId { get; set; } = default!;
    public string Label { get; set; } = default!;
    public NodePosition Position { get; set; } = new();
    public int TimeoutSeconds { get; set; } = 120;
    public float ConfidenceThreshold { get; set; } = 0.7f;
    public string? PinnedVersion { get; set; }
}
