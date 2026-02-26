using ESS.Domain.Entities;
using ESS.Domain.Abstractions;
using ESS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;


namespace ESS.Infrastructure.Repositories
{
    public class OracleTokenRepository : IValidateCodeRepository
    {
        private readonly ApplicationDbContext _db;

        public OracleTokenRepository(ApplicationDbContext db)
        {
            _db = db;
        }

        public async Task<EssSoftTokens?> FindAsync(int empCode, string authenticationCode, CancellationToken ct = default)
        {
            return await _db.Set<EssSoftTokens>()
                .FirstOrDefaultAsync(t => t.EmpCode == empCode
                                       && t.AuthenticationCode == authenticationCode
                                       && t.Status == 1, ct);
        }
    }
}
