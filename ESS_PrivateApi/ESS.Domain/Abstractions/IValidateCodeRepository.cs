using ESS.Domain.Entities;

namespace ESS.Domain.Abstractions
{
    public interface IValidateCodeRepository
    {
        Task<EssSoftTokens?> FindAsync(int empCode, string authenticationCode, CancellationToken ct = default);
    }
}
