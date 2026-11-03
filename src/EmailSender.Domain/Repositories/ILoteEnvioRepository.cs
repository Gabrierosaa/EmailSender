using EmailSender.Domain.Entities;
using EmailSender.Domain.Enum;

namespace EmailSender.Domain.Repositories
{
    public interface ILoteEnvioRepository
    {
        Task<LoteEnvio?> ObterPorIdAsync(Guid id, CancellationToken ct);
        Task<LoteEnvio?> ObterPorIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct);
        Task AdicionarAsync(LoteEnvio lote, CancellationToken ct);
        Task<ResumoLote?> ObterResumoAsync(Guid id, CancellationToken ct);
    }

    public sealed record ResumoLote(
        Guid Id,
        DateTimeOffset CriadoEm,
        IReadOnlyDictionary<EmailStatusEnum, int> ContagemPorStatus);
}
