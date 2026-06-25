namespace Axon.Core.DTOs.Deliveries;

// Registers a step as actually starting, with an accurate StartedAt — sent by the
// executor right before it begins building context/calling the agent for a node, BEFORE
// any work happens. AppendStep (POST /steps) then finalizes this same step in place
// rather than appending a second entry, so StartedAt/CompletedAt reflect real wall-clock
// times instead of both being stamped at the same append-time instant.
public class StartStepRequest
{
    public string NodeId { get; set; } = default!;
    public string BlockId { get; set; } = default!;
    public string BlockName { get; set; } = default!;
}
