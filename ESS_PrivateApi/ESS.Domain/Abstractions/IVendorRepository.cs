using ESS.Domain.Entities;

namespace ESS.Domain.Abstractions
{
    public interface IVendorRepository
    {
        Task<Vendor?> GetByApiKeyAsync(string apiKey);
        Task<Vendor?> GetByVendorIdAsync(string VendorId);
    }
}
