using EmailSender.Domain.Entities;
using EmailSender.Domain.Enum;
using EmailSender.Domain.Repositories;

namespace EmailSender.Application.Tests
{
    internal sealed class FakeEmailRepository : IEmailRepository
    {
        private readonly List<Email> _itens = new();

        public void Adicionar(Email email) => _itens.Add(email);

        public Task<Email?> ObterPorIdAsync(Guid id, CancellationToken ct) =>
            Task.FromResult(_itens.SingleOrDefault(e => e.Id == id));

        public Task<IReadOnlyList<Email>> ObterPorStatusAsync(EmailStatusEnum status, int limite, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<Email>>(_itens.Where(e => e.Status == status).Take(limite).ToList());

        public Task<IReadOnlyList<Email>> ObterPendentesParaEnvioAsync(DateTimeOffset agora, int limite, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<Email>>(_itens
                .Where(e => e.Status == EmailStatusEnum.Pendente && (e.ProximaTentativaEm is null || e.ProximaTentativaEm <= agora))
                .Take(limite).ToList());

        public Task AdicionarAsync(Email email, CancellationToken ct)
        {
            _itens.Add(email);
            return Task.CompletedTask;
        }
    }
}
