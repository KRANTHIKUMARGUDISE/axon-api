namespace Axon.Core.DTOs.Auth;

public class LoginResponse
{
    public string AccessToken { get; set; } = default!;
    public string RefreshToken { get; set; } = default!;
    public UserDto User { get; set; } = default!;
}
