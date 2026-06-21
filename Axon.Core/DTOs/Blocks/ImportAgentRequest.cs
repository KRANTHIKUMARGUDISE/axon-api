using Axon.Core.Enums;

namespace Axon.Core.DTOs.Blocks;

public class ImportAgentRequest
{
    public string Path { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public BlockRole Role { get; set; }
    public List<string> Tags { get; set; } = [];
}
