using Axon.Core.DTOs.Pipelines;
using Axon.Core.Interfaces;
using Axon.Core.Models;

namespace Axon.Core.Services;

public class PipelineValidator
{
    private readonly IBlockRepository _blocks;

    public PipelineValidator(IBlockRepository blocks) => _blocks = blocks;

    public async Task<ValidateResponse> ValidateAsync(List<PipelineNode> nodes, List<PipelineEdge> edges)
    {
        var errors = new List<string>();

        if (nodes.Count == 0)
        {
            errors.Add("Pipeline must have at least one node.");
            return new ValidateResponse { IsValid = false, Errors = errors };
        }

        var nodeIds = nodes.Select(n => n.Id).ToHashSet();

        // Edge node references
        foreach (var edge in edges)
        {
            if (!nodeIds.Contains(edge.SourceNodeId))
                errors.Add($"Edge '{edge.Id}' references unknown source node '{edge.SourceNodeId}'.");
            if (!nodeIds.Contains(edge.TargetNodeId))
                errors.Add($"Edge '{edge.Id}' references unknown target node '{edge.TargetNodeId}'.");
        }

        var inDegree = nodes.ToDictionary(n => n.Id, _ => 0);
        var outDegree = nodes.ToDictionary(n => n.Id, _ => 0);

        foreach (var edge in edges)
        {
            if (nodeIds.Contains(edge.SourceNodeId)) outDegree[edge.SourceNodeId]++;
            if (nodeIds.Contains(edge.TargetNodeId)) inDegree[edge.TargetNodeId]++;
        }

        var startNodes = nodes.Where(n => inDegree[n.Id] == 0).ToList();
        var endNodes = nodes.Where(n => outDegree[n.Id] == 0).ToList();

        if (startNodes.Count != 1)
            errors.Add($"Pipeline must have exactly one START node (no incoming edges); found {startNodes.Count}.");

        if (endNodes.Count != 1)
            errors.Add($"Pipeline must have exactly one END node (no outgoing edges); found {endNodes.Count}.");

        // Multiple outgoing edges: at most one IsDefault
        foreach (var node in nodes)
        {
            var outEdges = edges.Where(e => e.SourceNodeId == node.Id).ToList();
            if (outEdges.Count > 1 && outEdges.Count(e => e.IsDefault) > 1)
                errors.Add($"Node '{node.Id}' has more than one default outgoing edge.");
        }

        // Cycle detection via DFS
        if (DetectCycle(nodes, edges, nodeIds))
            errors.Add("Pipeline contains a cycle.");

        // Orphan nodes: reachable from start
        if (startNodes.Count == 1)
        {
            var reachable = Reachable(startNodes[0].Id, edges);
            var orphans = nodeIds.Except(reachable).ToList();
            foreach (var orphan in orphans)
                errors.Add($"Node '{orphan}' is not reachable from the START node.");
        }

        // Block existence
        foreach (var node in nodes)
        {
            var block = await _blocks.GetByIdAsync(node.BlockId);
            if (block == null)
                errors.Add($"Node '{node.Id}' references block '{node.BlockId}' which does not exist.");
        }

        return new ValidateResponse { IsValid = errors.Count == 0, Errors = errors };
    }

    private static bool DetectCycle(List<PipelineNode> nodes, List<PipelineEdge> edges, HashSet<string> nodeIds)
    {
        var adj = nodes.ToDictionary(n => n.Id, _ => new List<string>());
        foreach (var edge in edges)
            if (nodeIds.Contains(edge.SourceNodeId) && nodeIds.Contains(edge.TargetNodeId))
                adj[edge.SourceNodeId].Add(edge.TargetNodeId);

        var visited = new HashSet<string>();
        var inStack = new HashSet<string>();

        bool Dfs(string id)
        {
            visited.Add(id);
            inStack.Add(id);
            foreach (var next in adj[id])
            {
                if (!visited.Contains(next) && Dfs(next)) return true;
                if (inStack.Contains(next)) return true;
            }
            inStack.Remove(id);
            return false;
        }

        return nodes.Any(n => !visited.Contains(n.Id) && Dfs(n.Id));
    }

    private static HashSet<string> Reachable(string startId, List<PipelineEdge> edges)
    {
        var visited = new HashSet<string>();
        var queue = new Queue<string>();
        queue.Enqueue(startId);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (!visited.Add(current)) continue;
            foreach (var edge in edges.Where(e => e.SourceNodeId == current))
                queue.Enqueue(edge.TargetNodeId);
        }
        return visited;
    }
}
