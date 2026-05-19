namespace ESS.Domain.Entities
{
    public class RefreshToken
    {
        public int Id { get; set; }
        public string VendorId { get; set; } = default!;
        public string Token { get; set; } = default!;
        public DateTime ExpiryDate { get; set; }
        public bool IsRevoked { get; set; }
    }
}
