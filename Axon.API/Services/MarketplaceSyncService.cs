using Axon.Core.Config;
using Axon.Core.DTOs.Blocks;
using Axon.Core.Enums;
using Axon.Core.Interfaces;
using Axon.Infrastructure.Services;
using Microsoft.Extensions.Options;

namespace Axon.API.Services;

public class MarketplaceSyncService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeSpan _interval;
    private readonly ILogger<MarketplaceSyncService> _logger;

    public MarketplaceSyncService(IServiceScopeFactory scopeFactory, IOptions<MarketplaceConfig> config, ILogger<MarketplaceSyncService> logger)
    {
        _scopeFactory = scopeFactory;
        _interval = TimeSpan.FromHours(config.Value.CacheTtlHours);
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await RunSyncAsync(stoppingToken);
            await Task.Delay(_interval, stoppingToken).ContinueWith(_ => { }, CancellationToken.None);
        }
    }

    private async Task RunSyncAsync(CancellationToken ct)
    {
        _logger.LogInformation("Marketplace sync starting");
        int synced = 0, stale = 0;

        using var scope = _scopeFactory.CreateScope();
        var blocks = scope.ServiceProvider.GetRequiredService<IBlockRepository>();
        var marketplace = scope.ServiceProvider.GetRequiredService<IMarketplaceService>();

        var syncedBlocks = await blocks.GetAllAsync(new BlockFilter { SyncStatus = SyncStatus.Synced, IsActive = true });

        foreach (var block in syncedBlocks)
        {
            if (ct.IsCancellationRequested) break;
            if (block.MarketplacePath is null) continue;
            if (block.SourceType == SourceType.Axon || block.SourceType == SourceType.Local) continue;

            try
            {
                var definition = await marketplace.FetchBlockDefinitionAsync(block.MarketplacePath);
                if (definition is null)
                {
                    await blocks.UpdateSyncAsync(block.Id, SyncStatus.Stale);
                    stale++;
                }
                else
                {
                    if (block.CachedFiles != null && block.EntryPointPath != null)
                    {
                        var entryFile = block.CachedFiles.FirstOrDefault(f => f.RelativePath == block.EntryPointPath);
                        if (entryFile != null) entryFile.Content = definition;
                    }
                    await blocks.UpdateSyncAsync(block.Id, SyncStatus.Synced);
                    synced++;
                }
            }
            catch (MarketplaceUnavailableException ex)
            {
                _logger.LogWarning("GitHub unavailable for block {Id} ({Name}): {Message}", block.Id, block.Name, ex.Message);
                await blocks.UpdateSyncAsync(block.Id, SyncStatus.Stale);
                stale++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error syncing block {Id} ({Name})", block.Id, block.Name);
                stale++;
            }
        }

        _logger.LogInformation("Marketplace sync complete: {Synced} synced, {Stale} stale", synced, stale);
    }
}
