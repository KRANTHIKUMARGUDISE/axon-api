using System.Security.Claims;
using Axon.Core.DTOs.Deliveries;
using Axon.Core.Enums;
using Axon.Core.Interfaces;
using Axon.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;

namespace Axon.API.Controllers;

[ApiController]
[Route("api/deliveries")]
[Authorize]
public class DeliveriesController : ControllerBase
{
    private readonly IDeliveryRepository _deliveries;
    private readonly IPipelineRepository _pipelines;
    private readonly IUserRepository _users;
    private readonly IDeliveryHub _hub;

    public DeliveriesController(IDeliveryRepository deliveries, IPipelineRepository pipelines, IUserRepository users, IDeliveryHub hub)
    {
        _deliveries = deliveries;
        _pipelines = pipelines;
        _users = users;
        _hub = hub;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] DeliveryStatus? status)
    {
        var list = await _deliveries.GetAllByUserAsync(UserId(), status);
        return Ok(list.Select(ToSummary));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id)
    {
        var delivery = await _deliveries.GetByIdAsync(id, UserId());
        if (delivery == null) return NotFound();
        return Ok(ToDetail(delivery));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateDeliveryRequest request)
    {
        var pipeline = await _pipelines.GetByIdAsync(request.PipelineId);
        if (pipeline == null) return BadRequest(new { error = "Pipeline not found" });

        var userId = UserId();
        var user = await _users.GetByIdAsync(userId);
        if (user == null) return BadRequest(new { error = "User not found" });

        var attemptNumber = 1;
        if (request.RetriedFromDeliveryId != null)
        {
            var original = await _deliveries.GetByIdAsync(request.RetriedFromDeliveryId, userId);
            if (original != null) attemptNumber = original.AttemptNumber + 1;
        }

        var jobNumber = await _deliveries.GetNextJobNumberAsync();

        var delivery = new Delivery
        {
            Id = ObjectId.GenerateNewId().ToString(),
            TicketId = request.TicketId,
            TicketTitle = request.TicketTitle,
            PipelineId = request.PipelineId,
            PipelineSnapshot = pipeline,
            RepoUrl = request.RepoUrl,
            WorkspaceType = request.WorkspaceType,
            StoreFullContext = request.StoreFullContext,
            Inputs = request.Inputs,
            RetriedFromDeliveryId = request.RetriedFromDeliveryId,
            AttemptNumber = attemptNumber,
            JobNumber = jobNumber,
            Status = DeliveryStatus.Pending,
            CreatedBy = userId,
            CreatedByName = user.DisplayName,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _deliveries.CreateAsync(delivery);
        return CreatedAtAction(nameof(GetById), new { id = delivery.Id }, ToDetail(delivery));
    }

    [HttpPatch("{id}")]
    public async Task<IActionResult> Update(string id, [FromBody] UpdateDeliveryRequest request)
    {
        var delivery = await _deliveries.GetByIdAsync(id, UserId());
        if (delivery == null) return NotFound();
        if (delivery.Status != DeliveryStatus.Pending)
            return BadRequest(new { error = "Delivery can only be edited while Pending" });

        await _deliveries.UpdateAsync(id, request.RepoUrl, request.WorkspaceType, request.WorkspacePath);
        return NoContent();
    }

    [HttpPatch("{id}/status")]
    public async Task<IActionResult> UpdateStatus(string id, [FromBody] UpdateStatusRequest request)
    {
        var delivery = await _deliveries.GetByIdAsync(id, UserId());
        if (delivery == null) return NotFound();

        DateTime? startedAt = (request.Status == DeliveryStatus.Running && delivery.StartedAt == null)
            ? DateTime.UtcNow
            : null;

        await _deliveries.UpdateStatusAsync(id, request.Status, request.CurrentNodeId, request.WorkspacePath, request.TicketTitle, startedAt, request.Branch);
        return NoContent();
    }

    // DS1 safety net: Desktop is responsible for truncating large context/output before
    // sending (see executor.ts), but reject anything that slipped through over this hard cap.
    private const long MaxStepPayloadBytes = 500_000;

    [HttpPost("{id}/steps")]
    public async Task<IActionResult> AppendStep(string id, [FromBody] AppendStepRequest request)
    {
        if (Request.ContentLength is > MaxStepPayloadBytes)
        {
            return StatusCode(413, new { error = $"Step payload exceeds {MaxStepPayloadBytes} byte limit. Desktop should truncate large context/output before sending (DS1)." });
        }

        var delivery = await _deliveries.GetByIdAsync(id, UserId());
        if (delivery == null) return NotFound();

        // Desktop posts a step's full result in one shot after the agent has already
        // run (no separate start-then-finish signal reaches the backend), so
        // StartedAt/CompletedAt are both stamped at append time — except for a gate
        // step (AwaitingApproval), which isn't actually finished yet; that one gets
        // CompletedAt later, in UpdateGateStatusAsync, when it's approved/rejected.
        var now = DateTime.UtcNow;
        var step = new DeliveryStep
        {
            NodeId = request.NodeId,
            BlockId = request.BlockId,
            BlockName = request.BlockName,
            Status = request.Status,
            ContextSnapshot = request.ContextSnapshot,
            IsContextTruncated = request.IsContextTruncated,
            ContextFileRef = request.ContextFileRef,
            ContextAvailability = request.ContextAvailability,
            OutputAvailability = request.OutputAvailability,
            Output = request.Output,
            StartedAt = now,
            CompletedAt = request.Status == StepStatus.AwaitingApproval ? null : now
        };

        await _deliveries.AppendStepAsync(id, step);
        return NoContent();
    }

    [HttpPost("{id}/gate/approve")]
    public async Task<IActionResult> GateApprove(string id, [FromBody] GateDecisionRequest request)
    {
        var delivery = await _deliveries.GetByIdAsync(id, UserId());
        if (delivery == null) return NotFound();
        if (delivery.CurrentNodeId == null) return BadRequest(new { error = "No active gate node" });

        var userId = UserId();
        var user = await _users.GetByIdAsync(userId);
        await _deliveries.UpdateGateStatusAsync(id, delivery.CurrentNodeId, true, request.Reason, userId, user?.DisplayName);
        await _hub.SendGateApprovedAsync(id, delivery.CurrentNodeId);
        return NoContent();
    }

    [HttpPost("{id}/gate/reject")]
    public async Task<IActionResult> GateReject(string id, [FromBody] GateDecisionRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
            return BadRequest(new { error = "A comment is required to reject" });

        var delivery = await _deliveries.GetByIdAsync(id, UserId());
        if (delivery == null) return NotFound();
        if (delivery.CurrentNodeId == null) return BadRequest(new { error = "No active gate node" });

        var userId = UserId();
        var user = await _users.GetByIdAsync(userId);
        await _deliveries.UpdateGateStatusAsync(id, delivery.CurrentNodeId, false, request.Reason, userId, user?.DisplayName);
        await _hub.SendGateRejectedAsync(id, delivery.CurrentNodeId, request.Reason);
        return NoContent();
    }

    private string UserId() =>
        User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub") ?? string.Empty;

    private static DeliverySummaryDto ToSummary(Delivery d) => new()
    {
        Id = d.Id,
        TicketId = d.TicketId,
        TicketTitle = d.TicketTitle,
        PipelineId = d.PipelineId,
        PipelineName = d.PipelineSnapshot?.Name ?? string.Empty,
        Status = d.Status,
        WorkspaceType = d.WorkspaceType,
        RepoUrl = d.RepoUrl,
        StepCount = d.Steps.Count,
        CompletedSteps = d.Steps.Count(s => s.Status == StepStatus.Completed),
        JobNumber = d.JobNumber,
        OwnerTeam = d.OwnerTeam,
        CreatedBy = d.CreatedBy,
        CreatedByName = d.CreatedByName,
        CreatedAt = d.CreatedAt,
        CompletedAt = d.CompletedAt
    };

    private static DeliveryDetailDto ToDetail(Delivery d) => new()
    {
        Id = d.Id,
        TicketId = d.TicketId,
        TicketTitle = d.TicketTitle,
        PipelineId = d.PipelineId,
        PipelineSnapshot = d.PipelineSnapshot,
        Status = d.Status,
        WorkspaceType = d.WorkspaceType,
        StoreFullContext = d.StoreFullContext,
        RepoUrl = d.RepoUrl,
        CurrentNodeId = d.CurrentNodeId,
        WorkspacePath = d.WorkspacePath,
        Branch = d.Branch,
        Steps = d.Steps.Select(ToStepSummary).ToList(),
        Inputs = d.Inputs,
        RetriedFromDeliveryId = d.RetriedFromDeliveryId,
        AttemptNumber = d.AttemptNumber,
        JobNumber = d.JobNumber,
        OwnerTeam = d.OwnerTeam,
        CreatedBy = d.CreatedBy,
        CreatedByName = d.CreatedByName,
        CreatedAt = d.CreatedAt,
        UpdatedAt = d.UpdatedAt,
        StartedAt = d.StartedAt,
        CompletedAt = d.CompletedAt
    };

    // Omits ContextSnapshot and Output.Result — the two fields that can be
    // arbitrarily large. Fetched lazily via GetStepContext/GetStepOutput below.
    private static DeliveryStepSummaryDto ToStepSummary(DeliveryStep s) => new()
    {
        NodeId = s.NodeId,
        BlockId = s.BlockId,
        BlockName = s.BlockName,
        Status = s.Status,
        IsContextTruncated = s.IsContextTruncated,
        ContextFileRef = s.ContextFileRef,
        ContextAvailability = s.ContextAvailability,
        OutputAvailability = s.OutputAvailability,
        Output = s.Output == null ? null : new AgentOutputSummaryDto
        {
            Status = s.Output.Status,
            Confidence = s.Output.Confidence,
            Summary = s.Output.Summary,
            HumanGateReason = s.Output.HumanGateReason,
            ErrorMessage = s.Output.ErrorMessage,
            ErrorCode = s.Output.ErrorCode,
            Recoverable = s.Output.Recoverable,
            ApprovedBy = s.Output.ApprovedBy,
            ApprovedAt = s.Output.ApprovedAt,
            RejectedBy = s.Output.RejectedBy,
            RejectedAt = s.Output.RejectedAt,
            ReviewComment = s.Output.ReviewComment,
            IsTruncated = s.Output.IsTruncated,
            ToolsUsed = s.Output.ToolsUsed,
            ToolsDenied = s.Output.ToolsDenied,
            OutputFileRef = s.Output.OutputFileRef
        },
        StartedAt = s.StartedAt,
        CompletedAt = s.CompletedAt
    };

    [HttpGet("{deliveryId}/steps/{stepId}/context")]
    public async Task<IActionResult> GetStepContext(string deliveryId, string stepId)
    {
        var delivery = await _deliveries.GetByIdAsync(deliveryId, UserId());
        if (delivery == null) return NotFound();

        var step = delivery.Steps.FirstOrDefault(s => s.NodeId == stepId);
        if (step == null) return NotFound();

        if (step.IsContextTruncated)
            return Ok(new { truncated = true, fileRef = step.ContextFileRef });

        return Ok(step.ContextSnapshot ?? new BsonDocument());
    }

    [HttpGet("{deliveryId}/steps/{stepId}/output")]
    public async Task<IActionResult> GetStepOutput(string deliveryId, string stepId)
    {
        var delivery = await _deliveries.GetByIdAsync(deliveryId, UserId());
        if (delivery == null) return NotFound();

        var step = delivery.Steps.FirstOrDefault(s => s.NodeId == stepId);
        if (step == null || step.Output == null) return NotFound();

        if (step.Output.IsTruncated)
            return Ok(new { truncated = true, fileRef = step.Output.OutputFileRef });

        return Ok(step.Output.Result);
    }
}
