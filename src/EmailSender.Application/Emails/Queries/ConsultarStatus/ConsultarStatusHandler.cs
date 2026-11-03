using EmailSender.Application.DTOs;
using EmailSender.Domain.Repositories;

namespace EmailSender.Application.Emails.Queries.ConsultarStatus
{
    public sealed class ConsultarStatusHandler
    {
        private readonly IEmailRepository _emails;

        public ConsultarStatusHandler(IEmailRepository emails) => _emails = emails;

        public async Task<EmailStatusDto?> HandleAsync(ConsultarStatusQuery query, CancellationToken ct)
        {
            var email = await _emails.ObterPorIdAsync(query.Id, ct);
            if (email is null) return null;

            var ultima = email.Tentativas.MaxBy(t => t.Numero);

            return new EmailStatusDto(
                email.Id,
                email.Status.ToString(),
                ultima?.IniciadaEm,
                ultima is { Sucesso: false } ? ultima.CodigoErro : null);
        }
    }
}
