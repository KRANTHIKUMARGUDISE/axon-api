namespace Axon.Core.Enums;

public enum StepStatus
{
    Pending,
    Running,
    Completed,
    Failed,
    Paused,
    // Desktop's executor.ts sends this literal for a Control/gate node awaiting
    // approval — Paused above was never actually used for steps (only
    // Delivery.Status uses that name); this fixes the mismatch.
    AwaitingApproval
}
