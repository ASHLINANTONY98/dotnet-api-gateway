namespace ESS.Domain.Entities
{
    public class Vendor
    {
        public string VendorId { get; set; } = default!;
        public string VendorName { get; set; } = default!;
        public string ApiKey { get; set; } = default!;
        public int IsActive { get; set; }
        public string VendorRole { get; set; } = default!;


    }
}
