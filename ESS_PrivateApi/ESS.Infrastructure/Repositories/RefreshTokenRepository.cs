using ESS.Domain.Abstractions;
using ESS.Domain.Entities;
using ESS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ESS.Infrastructure.Repositories
{
    public class RefreshTokenRepository(ApplicationDbContext db) : IRefreshTokenRepository
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
                .FirstOrDefaultAsync(x => x.Token == token);
        }

        public async Task UpdateAsync(RefreshToken token)
        {
            _db.RefreshTokens.Update(token);
            await _db.SaveChangesAsync();
        }

    }
}
