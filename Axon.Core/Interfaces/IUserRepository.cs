using Axon.Core.Models;

namespace Axon.Core.Interfaces;

public interface IUserRepository
{
    Task<List<User>> GetAllAsync();
    Task<User?> GetByEmailAsync(string email);
    Task<User?> GetByIdAsync(string id);
    Task<User?> GetByRefreshTokenHashAsync(string hash);
    Task<User> CreateAsync(User user);
    Task UpdateLastLoginAsync(string id, DateTime loginAt);
    Task UpdateRefreshTokenAsync(string id, string? refreshTokenHash);
}
