namespace Axon.Core.Models;

public class MigrationRun
{
    public string Id { get; set; } = default!;
    public string Name { get; set; } = default!;
    public DateTime RanAt { get; set; }
}
