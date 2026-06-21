using System.Security.Claims;
using Axon.Core.DTOs.Pipelines;
using Axon.Core.Enums;
using Axon.Core.Interfaces;
using Axon.Core.Models;
using Axon.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;

namespace Axon.API.Controllers;

[ApiController]
[Route("api/pipelines")]
[Authorize]
public class PipelinesController : ControllerBase
{
    private readonly IPipelineRepository _pipelines;
    private readonly IUserRepository _users;
    private readonly PipelineValidator _validator;

    public PipelinesController(IPipelineRepository pipelines, IUserRepository users, PipelineValidator validator)
    {
        _pipelines = pipelines;
        _users = users;
        _validator = validator;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] PipelineVisibility? visibility)
    {
        var list = await _pipelines.GetAllAsync(visibility, UserId());
        return Ok(list.Select(ToSummary));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id)
    {
        var pipeline = await _pipelines.GetByIdAsync(id);
        if (pipeline == null) return NotFound();
        if (!CanAccess(pipeline)) return Forbid();
        return Ok(ToDetail(pipeline));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreatePipelineRequest request)
    {
        var result = await _validator.ValidateAsync(request.Nodes, request.Edges);
        if (!result.IsValid) return BadRequest(result);

        var userId = UserId();
        var user = await _users.GetByIdAsync(userId);
        if (user == null) return BadRequest(new { error = "User not found" });

        var pipeline = new PipelineDefinition
        {
            Id = ObjectId.GenerateNewId().ToString(),
            Name = request.Name,
            Description = request.Description ?? string.Empty,
            Tags = request.Tags,
            Visibility = request.Visibility,
            Nodes = request.Nodes,
            Edges = request.Edges,
            Version = 1,
            CreatedBy = userId,
            CreatedByName = user.DisplayName,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _pipelines.CreateAsync(pipeline);
        return CreatedAtAction(nameof(GetById), new { id = pipeline.Id }, ToDetail(pipeline));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, [FromBody] UpdatePipelineRequest request)
    {
        var pipeline = await _pipelines.GetByIdAsync(id);
        if (pipeline == null) return NotFound();
        if (!await CanModify(pipeline)) return Forbid();

        var nodes = request.Nodes ?? pipeline.Nodes;
        var edges = request.Edges ?? pipeline.Edges;

        var result = await _validator.ValidateAsync(nodes, edges);
        if (!result.IsValid) return BadRequest(result);

        if (request.Name != null) pipeline.Name = request.Name;
        if (request.Description != null) pipeline.Description = request.Description;
        if (request.Tags != null) pipeline.Tags = request.Tags;
        if (request.Visibility.HasValue) pipeline.Visibility = request.Visibility.Value;
        pipeline.Nodes = nodes;
        pipeline.Edges = edges;

        await _pipelines.UpdateAsync(pipeline);
        return Ok(ToDetail(pipeline));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        var pipeline = await _pipelines.GetByIdAsync(id);
        if (pipeline == null) return NotFound();
        if (!await CanModify(pipeline)) return Forbid();

        await _pipelines.DeleteAsync(id);
        return NoContent();
    }

    [HttpPost("{id}/validate")]
    public async Task<IActionResult> Validate(string id, [FromBody] ValidateRequest request)
    {
        var result = await _validator.ValidateAsync(request.Nodes, request.Edges);
        return Ok(result);
    }

    private string UserId() =>
        User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub") ?? string.Empty;

    private bool CanAccess(PipelineDefinition pipeline) =>
        pipeline.Visibility != PipelineVisibility.Personal || pipeline.CreatedBy == UserId();

    private async Task<bool> CanModify(PipelineDefinition pipeline)
    {
        var userId = UserId();
        if (pipeline.CreatedBy == userId) return true;
        var user = await _users.GetByIdAsync(userId);
        return user?.Role == UserRole.Admin;
    }

    private static PipelineSummaryDto ToSummary(PipelineDefinition p) => new()
    {
        Id = p.Id,
        Name = p.Name,
        Description = p.Description,
        Tags = p.Tags,
        Version = p.Version,
        VersionLabel = p.VersionLabel,
        Visibility = p.Visibility,
        Nodes = p.Nodes,
        Edges = p.Edges,
        NodeCount = p.Nodes.Count,
        OwnerTeam = p.OwnerTeam,
        CreatedBy = p.CreatedBy,
        CreatedByName = p.CreatedByName,
        CreatedAt = p.CreatedAt
    };

    private static PipelineDetailDto ToDetail(PipelineDefinition p) => new()
    {
        Id = p.Id,
        Name = p.Name,
        Description = p.Description,
        Tags = p.Tags,
        Version = p.Version,
        VersionLabel = p.VersionLabel,
        Nodes = p.Nodes,
        Edges = p.Edges,
        Visibility = p.Visibility,
        CreatedBy = p.CreatedBy,
        CreatedByName = p.CreatedByName,
        TeamId = p.TeamId,
        OwnerTeam = p.OwnerTeam,
        CreatedAt = p.CreatedAt,
        UpdatedAt = p.UpdatedAt
    };
}
