using ESS.Domain.Entities;

namespace ESS.Domain.Abstractions
{
    public interface IRefreshTokenRepository
    {
        Task AddAsync(RefreshToken token);

        Task<RefreshToken?> GetByTokenAsync(string token);

        Task<bool> RotateAsync(
            string currentTokenHash,
            RefreshToken newToken,
            DateTime nowUtc);
    }
}