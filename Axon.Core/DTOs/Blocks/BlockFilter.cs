using Axon.Core.Enums;

namespace Axon.Core.DTOs.Blocks;

public class BlockFilter
{
    public BlockRole? Role { get; set; }
    public SyncStatus? SyncStatus { get; set; }
    public bool? IsActive { get; set; }
    public List<string>? Tags { get; set; }
    public string? Search { get; set; }
}
