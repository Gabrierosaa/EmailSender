using EmailSender.Domain.Entities;

namespace EmailSender.Domain.Repositories
{
    public interface ILoteEnvioRepository
    {
        Task<LoteEnvio?> ObterPorIdAsync(Guid id, CancellationToken ct);
        Task<LoteEnvio?> ObterPorIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct);
        Task AdicionarAsync(LoteEnvio lote, CancellationToken ct);
    }
}
