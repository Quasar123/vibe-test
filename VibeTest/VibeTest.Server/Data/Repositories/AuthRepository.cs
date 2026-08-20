using Microsoft.EntityFrameworkCore;
using VibeTest.Server.Models.Entities;

namespace VibeTest.Server.Data.Repositories;

public class AuthRepository(AppDbContext db) : IAuthRepository
{
    public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default) =>
        db.Users.FirstOrDefaultAsync(u => u.Email == email, cancellationToken);

    public Task<User?> GetByIdAsync(int userId, CancellationToken cancellationToken = default) =>
        db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

    public async Task AddUserAsync(User user, CancellationToken cancellationToken = default) =>
        await db.Users.AddAsync(user, cancellationToken);

    public Task<RefreshToken?> GetRefreshTokenAsync(string token, CancellationToken cancellationToken = default) =>
        db.RefreshTokens
            .Include(rt => rt.User)
            .FirstOrDefaultAsync(rt => rt.Token == token, cancellationToken);

    public async Task AddRefreshTokenAsync(RefreshToken refreshToken, CancellationToken cancellationToken = default) =>
        await db.RefreshTokens.AddAsync(refreshToken, cancellationToken);

    public Task RemoveRefreshTokenAsync(RefreshToken refreshToken, CancellationToken cancellationToken = default)
    {
        db.RefreshTokens.Remove(refreshToken);
        return Task.CompletedTask;
    }

    public Task<int> DeleteRefreshTokenByValueAsync(string token, CancellationToken cancellationToken = default) =>
        db.Database.ExecuteSqlRawAsync("""DELETE FROM refresh_tokens WHERE token = {0}""", token);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        db.SaveChangesAsync(cancellationToken);
}
