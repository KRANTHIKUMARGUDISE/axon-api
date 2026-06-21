using Axon.Core.Enums;

namespace Axon.Core.DTOs.Deliveries;

public class UpdateDeliveryRequest
{
    public string? RepoUrl { get; set; }
    public WorkspaceType? WorkspaceType { get; set; }
    public string? WorkspacePath { get; set; }
}
