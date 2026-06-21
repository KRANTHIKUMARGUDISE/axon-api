using Axon.Core.Enums;

namespace Axon.Core.Models;

public class User
{
    public string Id { get; set; } = default!;
    public string Email { get; set; } = default!;
    public string DisplayName { get; set; } = default!;
    public string Team { get; set; } = default!;
    public UserRole Role { get; set; }
    public string PasswordHash { get; set; } = default!;
    public string? RefreshTokenHash { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
}
