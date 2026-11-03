using EmailSender.Domain.Entities;
using EmailSender.Domain.Enum;

namespace EmailSender.Domain.Repositories
{
    public interface IEmailRepository
    {
        Task<Email?> ObterPorIdAsync(Guid id, CancellationToken ct);
        Task<IReadOnlyList<Email>> ObterPorStatusAsync(EmailStatusEnum status, int limite, CancellationToken ct);
        Task AdicionarAsync(Email email, CancellationToken ct);
    }
}
