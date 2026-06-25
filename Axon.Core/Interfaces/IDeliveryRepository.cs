using Axon.Core.Enums;
using Axon.Core.Models;

namespace Axon.Core.Interfaces;

public interface IDeliveryRepository
{
    Task<List<Delivery>> GetAllByUserAsync(string userId, DeliveryStatus? status);
    Task<Delivery?> GetByIdAsync(string id, string userId);
    Task CreateAsync(Delivery delivery);
    Task UpdateAsync(string id, string? repoUrl, WorkspaceType? workspaceType, string? workspacePath);
    Task UpdateStatusAsync(string id, DeliveryStatus status, string? currentNodeId, string? workspacePath, string? ticketTitle, DateTime? startedAt = null, string? branch = null);
    Task AppendStepAsync(string id, DeliveryStep step);
    Task UpdateStepAsync(string id, string nodeId, DeliveryStep updatedStep);
    Task UpdateGateStatusAsync(string id, string nodeId, bool approved, string? reason, string approvedByUserId, string? approvedByName);
    // Atomically-incrementing, globally unique job number (1, 2, 3...) — used in
    // branch naming (axon/job-{n}-{ticketId}) so jobs are easy to reference/sort.
    Task<int> GetNextJobNumberAsync();
    // Part 4 task 5 — distinct union of AgentOutput.ToolsUsed across every step that ran
    // this block, plus how many of those steps actually recorded tool usage (i.e. ran
    // Agentic). Used to suggest a restricted allowedTools list after exploration-mode runs.
    Task<(List<string> Tools, int RunCount)> GetToolsUsedForBlockAsync(string blockId);
}
