using ESS.Domain.Abstractions;
using ESS.Domain.Entities;
using ESS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ESS.Infrastructure.Repositories
{
    public class OracleVendorRepository(ApplicationDbContext db) : IVendorRepository
    {
        private readonly ApplicationDbContext _db = db;

        public async Task<Vendor?> GetByApiKeyAsync(string apiKey)
        {
            return await _db.Vendors
                .FirstOrDefaultAsync(v => v.ApiKey == apiKey && v.IsActive == 1);
        }
        public async Task<Vendor?> GetByVendorIdAsync(string VendorId)
        {
            return await _db.Vendors
                .FirstOrDefaultAsync(v => v.VendorId == VendorId && v.IsActive == 1);
        }
    }
}