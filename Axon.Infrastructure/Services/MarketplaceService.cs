using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Axon.Core.Config;
using Axon.Core.DTOs.Blocks;
using Axon.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Axon.Infrastructure.Services;

public class MarketplaceService : IMarketplaceService
{
    private readonly HttpClient _http;
    private readonly MarketplaceConfig _config;
    private readonly ILogger<MarketplaceService> _logger;

    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    public MarketplaceService(HttpClient http, IOptions<MarketplaceConfig> config, ILogger<MarketplaceService> logger)
    {
        _http = http;
        _config = config.Value;
        _logger = logger;

        _http.BaseAddress = new Uri("https://api.github.com/");
        _http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("axon-api", "1.0"));
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        if (!string.IsNullOrEmpty(_config.GithubToken))
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _config.GithubToken);
    }

    public async Task<List<MarketplaceAgentDto>> BrowseAsync()
    {
        var (owner, repo) = ParseRepo(_config.GithubRepo);
        HttpResponseMessage response;
        try
        {
            response = await _http.GetAsync($"repos/{owner}/{repo}/contents/");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "GitHub unreachable during BrowseAsync");
            throw new MarketplaceUnavailableException("GitHub is unreachable.");
        }

        if (response.StatusCode == HttpStatusCode.Forbidden || response.StatusCode == (HttpStatusCode)429)
            throw new MarketplaceUnavailableException("GitHub API rate limit reached.");

        if (!response.IsSuccessStatusCode)
            throw new MarketplaceUnavailableException($"GitHub returned {(int)response.StatusCode}.");

        var body = await response.Content.ReadAsStringAsync();
        var entries = JsonSerializer.Deserialize<List<GitHubContentEntry>>(body, _json) ?? [];

        var agents = new List<MarketplaceAgentDto>();
        foreach (var entry in entries.Where(e => e.Type == "dir"))
        {
            var description = await FetchFirstLineAsync(owner, repo, $"{entry.Path}/AGENT.md");
            agents.Add(new MarketplaceAgentDto
            {
                Name = entry.Name,
                Path = entry.Path,
                Description = description,
                LastUpdated = null
            });
        }
        return agents;
    }

    public async Task<string?> FetchBlockDefinitionAsync(string marketplacePath)
    {
        var (owner, repo) = ParseRepo(_config.GithubRepo);

        var agentMd = await FetchFileContentAsync(owner, repo, $"{marketplacePath}/AGENT.md");
        if (agentMd is null)
            return null;

        var promptMd = await FetchFileContentAsync(owner, repo, $"{marketplacePath}/prompt.md");

        return promptMd is null
            ? agentMd
            : $"{agentMd}\n\n---\n\n{promptMd}";
    }

    private async Task<string?> FetchFileContentAsync(string owner, string repo, string filePath)
    {
        HttpResponseMessage response;
        try
        {
            response = await _http.GetAsync($"repos/{owner}/{repo}/contents/{filePath}");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "GitHub unreachable fetching {Path}", filePath);
            throw new MarketplaceUnavailableException("GitHub is unreachable.");
        }

        if (response.StatusCode == HttpStatusCode.Forbidden || response.StatusCode == (HttpStatusCode)429)
            throw new MarketplaceUnavailableException("GitHub API rate limit reached.");

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        if (!response.IsSuccessStatusCode)
            throw new MarketplaceUnavailableException($"GitHub returned {(int)response.StatusCode}.");

        var body = await response.Content.ReadAsStringAsync();
        var file = JsonSerializer.Deserialize<GitHubFileEntry>(body, _json);
        if (file?.Content is null) return null;

        var decoded = Convert.FromBase64String(file.Content.Replace("\n", ""));
        return System.Text.Encoding.UTF8.GetString(decoded);
    }

    private async Task<string?> FetchFirstLineAsync(string owner, string repo, string filePath)
    {
        try
        {
            var content = await FetchFileContentAsync(owner, repo, filePath);
            return content?.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.TrimStart('#').Trim();
        }
        catch
        {
            return null;
        }
    }

    private static (string owner, string repo) ParseRepo(string githubRepo)
    {
        var parts = githubRepo.Split('/', 2);
        return parts.Length == 2 ? (parts[0], parts[1]) : ("", githubRepo);
    }

    private record GitHubContentEntry(string Name, string Path, string Type);
    private record GitHubFileEntry(string? Content);
}

public class MarketplaceUnavailableException(string message) : Exception(message);
