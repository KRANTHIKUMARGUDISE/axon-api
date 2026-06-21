using Axon.Core.Interfaces;
using Axon.Core.Models;
using Axon.Infrastructure.MongoDB;
using MongoDB.Driver;

namespace Axon.Infrastructure.Repositories;

public class MongoUserRepository : IUserRepository
{
    private readonly MongoContext _context;

    public MongoUserRepository(MongoContext context) => _context = context;

    public async Task<List<User>> GetAllAsync() =>
        await _context.Users.Find(Builders<User>.Filter.Empty).ToListAsync();

    public async Task<User?> GetByEmailAsync(string email) =>
        await _context.Users.Find(u => u.Email == email).FirstOrDefaultAsync();

    public async Task<User?> GetByIdAsync(string id) =>
        await _context.Users.Find(ById(id)).FirstOrDefaultAsync();

    public async Task<User?> GetByRefreshTokenHashAsync(string hash) =>
        await _context.Users.Find(u => u.RefreshTokenHash == hash).FirstOrDefaultAsync();

    public async Task<User> CreateAsync(User user)
    {
        await _context.Users.InsertOneAsync(user);
        return user;
    }

    public async Task UpdateLastLoginAsync(string id, DateTime loginAt)
    {
        var update = Builders<User>.Update.Set(u => u.LastLoginAt, loginAt);
        await _context.Users.UpdateOneAsync(ById(id), update);
    }

    public async Task UpdateRefreshTokenAsync(string id, string? refreshTokenHash)
    {
        var update = Builders<User>.Update.Set(u => u.RefreshTokenHash, refreshTokenHash);
        await _context.Users.UpdateOneAsync(ById(id), update);
    }

    private static FilterDefinition<User> ById(string id) =>
        Builders<User>.Filter.Eq("_id", id);
}
