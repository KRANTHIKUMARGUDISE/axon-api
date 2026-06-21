namespace Axon.Core.DTOs.Blocks;

public class CachedFileRequest
{
    public string RelativePath { get; set; } = default!;
    public string Content { get; set; } = default!;
}
