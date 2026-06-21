using System.Security.Claims;
using Axon.Core.DTOs.Blocks;
using Axon.Core.Enums;
using Axon.Core.Interfaces;
using Axon.Core.Models;
using Axon.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;

namespace Axon.API.Controllers;

[ApiController]
[Route("api/marketplace")]
[Authorize]
public class MarketplaceController : ControllerBase
{
    private readonly IMarketplaceService _marketplace;
    private readonly IBlockRepository _blocks;

    public MarketplaceController(IMarketplaceService marketplace, IBlockRepository blocks)
    {
        _marketplace = marketplace;
        _blocks = blocks;
    }

    [HttpGet("browse")]
    public async Task<IActionResult> Browse()
    {
        try
        {
            var agents = await _marketplace.BrowseAsync();
            return Ok(agents);
        }
        catch (MarketplaceUnavailableException ex)
        {
            return StatusCode(503, new { error = ex.Message });
        }
    }

    [HttpPost("import")]
    public async Task<IActionResult> Import([FromBody] ImportAgentRequest request)
    {
        string? definition;
        try
        {
            definition = await _marketplace.FetchBlockDefinitionAsync(request.Path);
        }
        catch (MarketplaceUnavailableException ex)
        {
            return StatusCode(503, new { error = ex.Message });
        }

        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                  ?? User.FindFirst("sub")?.Value
                  ?? "unknown";

        var artifactName = System.Text.RegularExpressions.Regex.Replace(
            request.Name.ToLowerInvariant(), @"[^\w-]", "-").Replace("--", "-").Trim('-');

        var cachedFiles = definition is not null
            ? new List<CachedFile>
            {
                new CachedFile { RelativePath = "SKILL.md", Content = definition }
            }
            : [];

        var block = new BuildingBlock
        {
            Id = ObjectId.GenerateNewId().ToString(),
            Name = request.Name,
            Description = request.Description ?? "",
            Role = request.Role,
            SourceType = SourceType.Local,
            ArtifactName = artifactName,
            Version = 1,
            AgentRuntime = AgentRuntime.Claude,
            ArtifactFormat = ArtifactFormat.Skill,
            CachedFiles = cachedFiles,
            EntryPointPath = definition is not null ? "SKILL.md" : null,
            Tags = request.Tags,
            MarketplaceSource = "github",
            MarketplacePath = request.Path,
            SyncStatus = definition is not null ? SyncStatus.Synced : SyncStatus.Missing,
            IsActive = true,
            CreatedBy = userId,
            CreatedByName = userId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _blocks.CreateAsync(block);
        return Ok(block);
    }
}
