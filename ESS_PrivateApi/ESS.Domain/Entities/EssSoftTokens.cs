namespace ESS.Domain.Entities
{
    public class EssSoftTokens
    {
        public int EmpCode { get; set; } // NUMBER → int
        public string AuthenticationCode { get; set; } = default!; // VARCHAR2(6) → string
        public DateTime GeneratedOn { get; set; } // DATE → DateTime
        public int Status { get; set; } // NUMBER(1) → int
    }
}
