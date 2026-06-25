using Axon.Core.DTOs.Blocks;
using Axon.Core.Enums;
using Axon.Core.Models;

namespace Axon.Core.Interfaces;

public interface IBlockRepository
{
    Task<List<BuildingBlock>> GetAllAsync(BlockFilter filter, string? userId);
    Task<BuildingBlock?> GetByIdAsync(string id);
    Task CreateAsync(BuildingBlock block);
    Task UpdateAsync(BuildingBlock block);
    Task DeactivateAsync(string id);
    Task UpdateSyncAsync(string id, SyncStatus status);
}
