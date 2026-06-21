using Axon.Core.Enums;

namespace Axon.Core.DTOs.Auth;

public class UserDto
{
    public string Id { get; set; } = default!;
    public string Email { get; set; } = default!;
    public string DisplayName { get; set; } = default!;
    public string Team { get; set; } = default!;
    public UserRole Role { get; set; }
}
