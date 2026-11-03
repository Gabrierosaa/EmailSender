using EmailSender.Application.Abstractions;
using EmailSender.Application.DTOs;
using EmailSender.Application.Emails.Commands.RegistrarEmail;
using EmailSender.Domain.Entities;
using EmailSender.Domain.Enum;
using EmailSender.Domain.Repositories;
using FluentValidation;

namespace EmailSender.Application.Tests
{
    public class RegistrarEmailHandlerTests
    {
        private readonly FakeLoteRepository _lotes = new();
        private readonly FakeUnitOfWork _unitOfWork = new();
        private readonly RegistrarEmailHandler _handler;

        public RegistrarEmailHandlerTests()
        {
            _handler = new RegistrarEmailHandler(_lotes, _unitOfWork, new CriarEmailRequestValidator());
        }

        private static CriarEmailRequest Valido() =>
            new("a@teste.com", "Assunto", "Remetente", "Fulano", "r@teste.com", "<p>oi</p>", "oi",
                new Dictionary<string, string> { ["nome"] = "Fulano" });

        [Fact]
        public async Task Handle_RequisicaoValida_DevePersistirLoteUnitarioPendente()
        {
            var resposta = await _handler.HandleAsync(Valido(), CancellationToken.None);

            var lote = Assert.Single(_lotes.Adicionados);
            var email = Assert.Single(lote.Emails);
            Assert.Equal(resposta.Id, email.Id);
            Assert.Equal("a@teste.com", email.Destinatario);
            Assert.Equal("Fulano", email.NomeDestinatario);
            Assert.Equal("{\"nome\":\"Fulano\"}", email.VariavelJson);
            Assert.Equal(EmailStatusEnum.Pendente, email.Status);
            Assert.Equal("Assunto", lote.Assunto);
            Assert.Equal("Remetente", lote.RemetenteNome);
            Assert.Equal("r@teste.com", lote.ReplyTo);
            Assert.Equal("<p>oi</p>", lote.CorpoHtml);
            Assert.Equal("oi", lote.CorpoTexto);
            Assert.Equal(1, _unitOfWork.Commits);
        }

        [Fact]
        public async Task Handle_SemVariaveis_DeveUsarJsonVazio()
        {
            var request = Valido() with { Variaveis = null };

            await _handler.HandleAsync(request, CancellationToken.None);

            Assert.Equal("{}", _lotes.Adicionados.Single().Emails.Single().VariavelJson);
        }

        [Fact]
        public async Task Handle_RequisicaoInvalida_DeveLancarValidationExceptionENaoPersistir()
        {
            var request = Valido() with { Destinatario = "invalido" };

            var excecao = await Assert.ThrowsAsync<ValidationException>(
                () => _handler.HandleAsync(request, CancellationToken.None));

            Assert.Contains(excecao.Errors, e => e.ErrorMessage == "O destinatário informado não é um e-mail válido.");
            Assert.Empty(_lotes.Adicionados);
            Assert.Equal(0, _unitOfWork.Commits);
        }

        [Theory]
        [InlineData("")]
        [InlineData("sem-arroba")]
        public async Task Handle_DestinatarioInvalido_DeveLancarValidationExceptionEmVezDeDomainException(string destinatario)
        {
            await AssertValidationAsync(Valido() with { Destinatario = destinatario });
        }

        [Fact]
        public async Task Handle_DestinatarioAcimaDe254_DeveLancarValidationException()
        {
            await AssertValidationAsync(Valido() with { Destinatario = new string('a', 250) + "@x.com" });
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public async Task Handle_AssuntoVazio_DeveLancarValidationException(string assunto)
        {
            await AssertValidationAsync(Valido() with { Assunto = assunto });
        }

        [Fact]
        public async Task Handle_AssuntoAcimaDe300_DeveLancarValidationException()
        {
            await AssertValidationAsync(Valido() with { Assunto = new string('a', 301) });
        }

        [Fact]
        public async Task Handle_RemetenteVazio_DeveLancarValidationException()
        {
            await AssertValidationAsync(Valido() with { RemetenteNome = "" });
        }

        [Fact]
        public async Task Handle_RemetenteAcimaDe200_DeveLancarValidationException()
        {
            await AssertValidationAsync(Valido() with { RemetenteNome = new string('a', 201) });
        }

        [Fact]
        public async Task Handle_SemCorpo_DeveLancarValidationException()
        {
            await AssertValidationAsync(Valido() with { CorpoHtml = null, CorpoTexto = " " });
        }

        private async Task AssertValidationAsync(CriarEmailRequest request)
        {
            await Assert.ThrowsAsync<ValidationException>(() => _handler.HandleAsync(request, CancellationToken.None));

            Assert.Empty(_lotes.Adicionados);
            Assert.Equal(0, _unitOfWork.Commits);
        }

        [Fact]
        public async Task Handle_DeveGerarIdsDiferentesACadaChamada()
        {
            var primeira = await _handler.HandleAsync(Valido(), CancellationToken.None);
            var segunda = await _handler.HandleAsync(Valido(), CancellationToken.None);

            Assert.NotEqual(primeira.Id, segunda.Id);
        }

        [Fact]
        public void Handler_NaoDeveDependerDePortaDeEnvio()
        {
            var tiposDasDependencias = typeof(RegistrarEmailHandler)
                .GetConstructors()
                .SelectMany(c => c.GetParameters())
                .Select(p => p.ParameterType)
                .ToArray();

            Assert.DoesNotContain(typeof(IEmailSender), tiposDasDependencias);
            Assert.All(tiposDasDependencias, t =>
                Assert.DoesNotContain("Smtp", t.Name, StringComparison.OrdinalIgnoreCase));
            Assert.All(tiposDasDependencias, t =>
                Assert.DoesNotContain("Rabbit", t.Name, StringComparison.OrdinalIgnoreCase));
        }

        private sealed class FakeLoteRepository : ILoteEnvioRepository
        {
            public List<LoteEnvio> Adicionados { get; } = new();

            public Task AdicionarAsync(LoteEnvio lote, CancellationToken ct)
            {
                Adicionados.Add(lote);
                return Task.CompletedTask;
            }

            public Task<LoteEnvio?> ObterPorIdAsync(Guid id, CancellationToken ct) =>
                Task.FromResult(Adicionados.FirstOrDefault(l => l.Id == id));

            public Task<ResumoLote?> ObterResumoAsync(Guid id, CancellationToken ct) =>
                Task.FromResult<ResumoLote?>(null);

            public Task<LoteEnvio?> ObterPorIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct) =>
                Task.FromResult(Adicionados.FirstOrDefault(l => l.IdempotencyKey == idempotencyKey));
        }

        private sealed class FakeUnitOfWork : IUnitOfWork
        {
            public int Commits { get; private set; }

            public Task<int> CommitAsync(CancellationToken ct)
            {
                Commits++;
                return Task.FromResult(1);
            }
        }
    }
}
