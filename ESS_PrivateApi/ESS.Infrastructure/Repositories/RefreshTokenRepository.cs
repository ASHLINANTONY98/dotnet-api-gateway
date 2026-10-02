using ESS.Domain.Abstractions;
using ESS.Domain.Entities;
using ESS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ESS.Infrastructure.Repositories
{
    public class RefreshTokenRepository(
        ApplicationDbContext db) : IRefreshTokenRepository
    {
        private readonly ApplicationDbContext _db = db;

        public async Task AddAsync(RefreshToken token)
        {
            _db.RefreshTokens.Add(token);
            await _db.SaveChangesAsync();
        }

        public async Task<RefreshToken?> GetByTokenAsync(string token)
        {
            return await _db.RefreshTokens
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Token == token);
        }

        public async Task<bool> RotateAsync(
            string currentTokenHash,
            RefreshToken newToken,
            DateTime nowUtc)
        {
            await using var transaction =
                await _db.Database.BeginTransactionAsync();

            // Atomically consume the old token.
            // Only one concurrent request can update it.
            var rowsUpdated = await _db.RefreshTokens
                .Where(x =>
                    x.Token == currentTokenHash &&
                    !x.IsRevoked &&
                    x.ExpiryDate > nowUtc)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.IsRevoked, true));

            if (rowsUpdated != 1)
            {
                await transaction.RollbackAsync();
                return false;
            }

            // Insert the replacement token in the same transaction.
            _db.RefreshTokens.Add(newToken);
            await _db.SaveChangesAsync();

            await transaction.CommitAsync();

            return true;
        }
    }
}