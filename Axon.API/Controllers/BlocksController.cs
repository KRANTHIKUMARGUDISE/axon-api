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
    private readonly IDeliveryRepository _deliveries;

    public BlocksController(IBlockRepository blocks, IUserRepository users, IMarketplaceService marketplace, IDeliveryRepository deliveries)
    {
        _blocks = blocks;
        _users = users;
        _marketplace = marketplace;
        _deliveries = deliveries;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] BlockRole? role,
        [FromQuery] SyncStatus? syncStatus,
        [FromQuery] string? search,
        [FromQuery] bool? isActive,
        [FromQuery] List<string>? tags,
        [FromQuery] BuildingBlockVisibility? visibility)
    {
        var filter = new BlockFilter { Role = role, SyncStatus = syncStatus, IsActive = isActive, Tags = tags, Search = search, Visibility = visibility };
        var blocks = await _blocks.GetAllAsync(filter, UserId());
        return Ok(blocks.Select(ToSummary));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id)
    {
        var block = await _blocks.GetByIdAsync(id);
        if (block == null) return NotFound();
        if (!CanAccess(block)) return Forbid();
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

        BuildingBlockVisibility? requestVisibility = request.SourceType == SourceType.Local ? request.Visibility ?? BuildingBlockVisibility.Personal : null;
        var executionError = ValidateExecutionProfile(request.SourceType, request.ExecutionType,
            request.AllowedTools, request.ExecutionAgreementAccepted, requestVisibility);
        if (executionError != null)
            return BadRequest(new { error = executionError });

        var userId = UserId();
        var user = await _users.GetByIdAsync(userId);
        if (user == null) return BadRequest(new { error = "User not found" });

        var isAgentic = request.ExecutionType == ExecutionType.Agentic;

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
            OutputMapping = request.OutputMapping,
            Tags = request.Tags,
            ExecutionType = request.ExecutionType,
            AllowedTools = request.AllowedTools,
            DefaultTimeoutSeconds = request.DefaultTimeoutSeconds,
            ExecutionAgreementAcceptedAt = isAgentic ? DateTime.UtcNow : null,
            ExecutionAgreementAcceptedBy = isAgentic ? user.DisplayName : null,
            Visibility = request.SourceType == SourceType.Local ? request.Visibility ?? BuildingBlockVisibility.Personal : null,
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

        var nextExecutionType = request.ExecutionType ?? block.ExecutionType;
        var nextAllowedTools = request.AllowedTools ?? block.AllowedTools;
        var executionProfileChanged = request.ExecutionType.HasValue && request.ExecutionType != block.ExecutionType
            || request.AllowedTools != null && !ToolsEqual(request.AllowedTools, block.AllowedTools);

        // If the profile changed, the prior agreement no longer covers it — require fresh confirmation.
        // If unchanged, the existing acceptance (or lack of it) still stands; a plain content edit needs no signal.
        var hasAgreementCoverage = executionProfileChanged
            ? request.ExecutionAgreementAccepted == true
            : request.ExecutionAgreementAccepted == true || block.ExecutionAgreementAcceptedAt.HasValue;

        var nextVisibility = request.Visibility ?? block.Visibility;
        var leavingPersonalUnscoped = block.Visibility == BuildingBlockVisibility.Personal
            && nextVisibility != BuildingBlockVisibility.Personal
            && nextExecutionType == ExecutionType.Agentic
            && (nextAllowedTools == null || nextAllowedTools.Count == 0);
        if (leavingPersonalUnscoped)
            return BadRequest(new { error = "Specify the tools this block requires before sharing or promoting it out of personal scope." });

        var executionError = ValidateExecutionProfile(block.SourceType, nextExecutionType, nextAllowedTools, hasAgreementCoverage, nextVisibility);
        if (executionError != null)
            return BadRequest(new { error = executionError });

        if (request.Name != null) block.Name = request.Name;
        if (request.Description != null) block.Description = request.Description;
        if (request.Role.HasValue) block.Role = request.Role.Value;
        if (request.ArtifactName != null) block.ArtifactName = request.ArtifactName;
        if (request.AgentRuntime.HasValue) block.AgentRuntime = request.AgentRuntime.Value;
        if (request.ArtifactFormat.HasValue) block.ArtifactFormat = request.ArtifactFormat.Value;
        if (request.ContextRequirements != null) block.ContextRequirements = request.ContextRequirements;
        if (request.OutputSchema != null) block.OutputSchema = request.OutputSchema;
        if (request.OutputMapping != null) block.OutputMapping = request.OutputMapping;
        if (request.Tags != null) block.Tags = request.Tags;
        if (request.CachedFiles != null)
            block.CachedFiles = request.CachedFiles.Select(f => new CachedFile
            {
                RelativePath = f.RelativePath,
                Content = f.Content
            }).ToList();
        if (request.EntryPointPath != null) block.EntryPointPath = request.EntryPointPath;
        if (request.Visibility.HasValue) block.Visibility = request.Visibility;
        if (request.ExecutionType.HasValue) block.ExecutionType = request.ExecutionType;
        if (request.AllowedTools != null) block.AllowedTools = request.AllowedTools;
        if (request.DefaultTimeoutSeconds.HasValue) block.DefaultTimeoutSeconds = request.DefaultTimeoutSeconds.Value;
        if (executionProfileChanged && request.ExecutionAgreementAccepted == true)
        {
            var acceptingUser = await _users.GetByIdAsync(UserId());
            block.ExecutionAgreementAcceptedAt = DateTime.UtcNow;
            block.ExecutionAgreementAcceptedBy = acceptingUser?.DisplayName;
        }

        await _blocks.UpdateAsync(block);
        return Ok(ToDetail(block));
    }

    [HttpGet("{id}/tools-used")]
    public async Task<IActionResult> GetToolsUsed(string id)
    {
        var block = await _blocks.GetByIdAsync(id);
        if (block == null) return NotFound();
        if (!CanAccess(block)) return Forbid();

        var (tools, runCount) = await _deliveries.GetToolsUsedForBlockAsync(id);
        return Ok(new { tools, runCount });
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

    private bool CanAccess(BuildingBlock block) =>
        block.Visibility != BuildingBlockVisibility.Personal || block.CreatedBy == UserId();

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

    private static string? ValidateExecutionProfile(SourceType sourceType, ExecutionType? executionType,
        List<string>? allowedTools, bool agreementAccepted, BuildingBlockVisibility? visibility)
    {
        if (sourceType == SourceType.Axon)
        {
            if (executionType.HasValue || allowedTools != null)
                return "Axon-sourced blocks cannot have an execution profile";
            return null;
        }

        if (executionType != ExecutionType.Agentic)
            return null;

        if (sourceType != SourceType.Local)
            return "Agentic execution is only supported for Local blocks";

        // Exploration mode: in personal scope, an empty tool list is allowed — the executor
        // falls back to the full six-tool ceiling rather than this being a misconfiguration.
        var isExploring = visibility == BuildingBlockVisibility.Personal;
        if (!isExploring && (allowedTools == null || allowedTools.Count == 0))
            return "Agentic blocks must specify at least one allowed tool";
        if (!agreementAccepted)
            return "Execution agreement must be accepted for Agentic blocks";

        return null;
    }

    private static bool ToolsEqual(List<string>? a, List<string>? b)
    {
        if (a == null || b == null) return a == b;
        return a.OrderBy(x => x).SequenceEqual(b.OrderBy(x => x));
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
        ContextRequirements = b.ContextRequirements,
        OutputSchema = b.OutputSchema,
        Tags = b.Tags,
        RunCount = b.RunCount,
        IsActive = b.IsActive,
        CreatedBy = b.CreatedBy,
        CreatedByName = b.CreatedByName,
        Visibility = b.Visibility,
        ExecutionType = b.ExecutionType,
        DefaultTimeoutSeconds = b.DefaultTimeoutSeconds
    };

    private static BlockDetailDto ToDetail(BuildingBlock b) => new()
    {
        Id = b.Id,
        Name = b.Name,
        Description = b.Description,
        Role = b.Role,
        ContextRequirements = b.ContextRequirements,
        OutputSchema = b.OutputSchema,
        OutputMapping = b.OutputMapping,
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
        Visibility = b.Visibility,
        ExecutionType = b.ExecutionType,
        AllowedTools = b.AllowedTools,
        DefaultTimeoutSeconds = b.DefaultTimeoutSeconds,
        ExecutionAgreementAcceptedAt = b.ExecutionAgreementAcceptedAt,
        ExecutionAgreementAcceptedBy = b.ExecutionAgreementAcceptedBy,
        CreatedAt = b.CreatedAt,
        UpdatedAt = b.UpdatedAt
    };
}
