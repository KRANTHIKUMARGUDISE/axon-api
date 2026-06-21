using System.Security.Claims;
using Axon.Core.DTOs.Blocks;
using Axon.Core.Enums;
using Axon.Core.Interfaces;
using Axon.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;

namespace Axon.API.Controllers;

[ApiController]
[Route("api/blocks")]
[Authorize]
public class BlocksController : ControllerBase
{
    private readonly IBlockRepository _blocks;
    private readonly IUserRepository _users;
    private readonly IMarketplaceService _marketplace;

    public BlocksController(IBlockRepository blocks, IUserRepository users, IMarketplaceService marketplace)
    {
        _blocks = blocks;
        _users = users;
        _marketplace = marketplace;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] BlockRole? role,
        [FromQuery] SyncStatus? syncStatus,
        [FromQuery] string? search,
        [FromQuery] bool? isActive,
        [FromQuery] List<string>? tags)
    {
        var filter = new BlockFilter { Role = role, SyncStatus = syncStatus, IsActive = isActive, Tags = tags, Search = search };
        var blocks = await _blocks.GetAllAsync(filter);
        return Ok(blocks.Select(ToSummary));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id)
    {
        var block = await _blocks.GetByIdAsync(id);
        if (block == null) return NotFound();
        return Ok(ToDetail(block));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateBlockRequest request)
    {
        if (request.SourceType == SourceType.Axon)
            return BadRequest(new { error = "Axon blocks are seeded by the system only" });

        var validationError = ValidateArtifact(request.SourceType, request.AgentRuntime,
            request.ArtifactFormat, request.CachedFiles, request.EntryPointPath);
        if (validationError != null)
            return BadRequest(new { error = validationError });

        var userId = UserId();
        var user = await _users.GetByIdAsync(userId);
        if (user == null) return BadRequest(new { error = "User not found" });

        var cachedFiles = request.CachedFiles?.Select(f => new CachedFile
        {
            RelativePath = f.RelativePath,
            Content = f.Content
        }).ToList();

        var block = new BuildingBlock
        {
            Id = ObjectId.GenerateNewId().ToString(),
            Name = request.Name,
            Description = request.Description,
            Role = request.Role,
            SourceType = request.SourceType,
            ArtifactName = request.ArtifactName,
            Version = 1,
            AgentRuntime = request.AgentRuntime,
            ArtifactFormat = request.ArtifactFormat,
            CachedFiles = cachedFiles,
            EntryPointPath = request.EntryPointPath,
            ContextRequirements = request.ContextRequirements,
            OutputSchema = request.OutputSchema,
            Tags = request.Tags,
            SyncStatus = SyncStatus.Local,
            IsActive = true,
            CreatedBy = userId,
            CreatedByName = user.DisplayName,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        await _blocks.CreateAsync(block);
        return CreatedAtAction(nameof(GetById), new { id = block.Id }, ToDetail(block));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, [FromBody] UpdateBlockRequest request)
    {
        var block = await _blocks.GetByIdAsync(id);
        if (block == null) return NotFound();

        if (block.SourceType == SourceType.Axon)
            return StatusCode(403, new { error = "System blocks are immutable" });

        if (!await CanModify(block)) return Forbid();

        if (request.Name != null) block.Name = request.Name;
        if (request.Description != null) block.Description = request.Description;
        if (request.Role.HasValue) block.Role = request.Role.Value;
        if (request.ArtifactName != null) block.ArtifactName = request.ArtifactName;
        if (request.AgentRuntime.HasValue) block.AgentRuntime = request.AgentRuntime.Value;
        if (request.ArtifactFormat.HasValue) block.ArtifactFormat = request.ArtifactFormat.Value;
        if (request.ContextRequirements != null) block.ContextRequirements = request.ContextRequirements;
        if (request.OutputSchema != null) block.OutputSchema = request.OutputSchema;
        if (request.Tags != null) block.Tags = request.Tags;
        if (request.CachedFiles != null)
            block.CachedFiles = request.CachedFiles.Select(f => new CachedFile
            {
                RelativePath = f.RelativePath,
                Content = f.Content
            }).ToList();
        if (request.EntryPointPath != null) block.EntryPointPath = request.EntryPointPath;

        await _blocks.UpdateAsync(block);
        return Ok(ToDetail(block));
    }

    [HttpPost("{id}/sync")]
    public async Task<IActionResult> Sync(string id)
    {
        var block = await _blocks.GetByIdAsync(id);
        if (block == null) return NotFound();

        if (block.SourceType == SourceType.Axon || block.SourceType == SourceType.Local)
            return Ok(new { message = "No sync required for this block type", syncStatus = block.SyncStatus });

        var definition = block.MarketplacePath != null
            ? await _marketplace.FetchBlockDefinitionAsync(block.MarketplacePath)
            : null;

        if (definition != null && block.CachedFiles != null)
        {
            var entryFile = block.CachedFiles.FirstOrDefault(f => f.RelativePath == block.EntryPointPath);
            if (entryFile != null)
                entryFile.Content = definition;
        }

        await _blocks.UpdateSyncAsync(id, SyncStatus.Synced);

        return Ok(new { message = "Sync complete", syncStatus = SyncStatus.Synced });
    }

    [HttpPost("{id}/deactivate")]
    public async Task<IActionResult> Deactivate(string id)
    {
        var block = await _blocks.GetByIdAsync(id);
        if (block == null) return NotFound();

        if (block.SourceType == SourceType.Axon)
            return StatusCode(403, new { error = "System blocks cannot be deactivated" });

        if (!await CanModify(block)) return Forbid();

        await _blocks.DeactivateAsync(id);
        return Ok(new { message = "Block deactivated" });
    }

    private string UserId() =>
        User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub") ?? string.Empty;

    private async Task<bool> CanModify(BuildingBlock block)
    {
        var userId = UserId();
        if (block.CreatedBy == userId) return true;
        var user = await _users.GetByIdAsync(userId);
        return user?.Role == UserRole.Admin;
    }

    private static string? ValidateArtifact(SourceType sourceType, AgentRuntime agentRuntime,
        ArtifactFormat artifactFormat, List<CachedFileRequest>? cachedFiles, string? entryPointPath)
    {
        if (sourceType == SourceType.Local)
        {
            if (artifactFormat != ArtifactFormat.Skill)
                return "Local blocks must use Skill format";
            if (cachedFiles == null || cachedFiles.Count == 0)
                return "Local blocks must have at least one cached file";
            if (string.IsNullOrEmpty(entryPointPath))
                return "Local blocks must specify an entry point path";
            if (!cachedFiles.Any(f => f.RelativePath == entryPointPath))
                return "Entry point path must match one of the cached file paths";
        }

        return null;
    }

    private static BlockSummaryDto ToSummary(BuildingBlock b) => new()
    {
        Id = b.Id,
        Name = b.Name,
        Description = b.Description,
        Role = b.Role,
        SourceType = b.SourceType,
        ArtifactName = b.ArtifactName,
        AgentRuntime = b.AgentRuntime,
        ArtifactFormat = b.ArtifactFormat,
        Tags = b.Tags,
        RunCount = b.RunCount,
        IsActive = b.IsActive,
        CreatedBy = b.CreatedBy,
        CreatedByName = b.CreatedByName
    };

    private static BlockDetailDto ToDetail(BuildingBlock b) => new()
    {
        Id = b.Id,
        Name = b.Name,
        Description = b.Description,
        Role = b.Role,
        ContextRequirements = b.ContextRequirements,
        OutputSchema = b.OutputSchema,
        Tags = b.Tags,
        RunCount = b.RunCount,
        CreatedBy = b.CreatedBy,
        CreatedByName = b.CreatedByName,
        SourceType = b.SourceType,
        ArtifactName = b.ArtifactName,
        Version = b.Version,
        AgentRuntime = b.AgentRuntime,
        ArtifactFormat = b.ArtifactFormat,
        CachedFiles = b.CachedFiles,
        EntryPointPath = b.EntryPointPath,
        MarketplaceSource = b.MarketplaceSource,
        MarketplacePath = b.MarketplacePath,
        MarketplaceVersion = b.MarketplaceVersion,
        SyncStatus = b.SyncStatus,
        IsActive = b.IsActive,
        CreatedAt = b.CreatedAt,
        UpdatedAt = b.UpdatedAt
    };
}
