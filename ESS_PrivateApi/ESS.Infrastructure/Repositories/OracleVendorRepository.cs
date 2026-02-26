using ESS.Domain.Abstractions;
using ESS.Domain.Entities;
using ESS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ESS.Infrastructure.Repositories
{
    public class OracleVendorRepository : IVendorRepository
    {
        private readonly ApplicationDbContext _db;

        public OracleVendorRepository(ApplicationDbContext db)
        {
            _db = db;
        }

        public async Task<Vendor?> GetByApiKeyAsync(string apiKey)
        {
            return await _db.Vendors
                .FirstOrDefaultAsync(v => v.ApiKey == apiKey && v.IsActive == 1);
        }
    }
}