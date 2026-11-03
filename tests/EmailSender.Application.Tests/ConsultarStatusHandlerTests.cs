using EmailSender.Application.Emails.Queries.ConsultarStatus;
using EmailSender.Domain.Entities;
using EmailSender.Domain.Enum;

namespace EmailSender.Application.Tests
{
    public class ConsultarStatusHandlerTests
    {
        private static readonly DateTimeOffset Base = new(2026, 11, 3, 10, 0, 0, TimeSpan.Zero);

        private readonly FakeEmailRepository _emails = new();
        private readonly ConsultarStatusHandler _handler;

        public ConsultarStatusHandlerTests()
        {
            _handler = new ConsultarStatusHandler(_emails);
        }

        private static Email NovoEmail() => new(Guid.NewGuid(), "a@teste.com", "Fulano", "{}");

        [Fact]
        public async Task Handle_SemTentativas_RetornaStatusComCamposNulos()
        {
            var email = NovoEmail();
            _emails.Adicionar(email);

            var resultado = await _handler.HandleAsync(new ConsultarStatusQuery(email.Id), CancellationToken.None);

            Assert.NotNull(resultado);
            Assert.Equal(email.Id, resultado.Id);
            Assert.Equal("Pendente", resultado.Status);
            Assert.Null(resultado.UltimaTentativaEm);
            Assert.Null(resultado.UltimoErro);
        }

        [Fact]
        public async Task Handle_VariasTentativas_UsaATentativaDeMaiorNumero()
        {
            var email = NovoEmail();
            email.Enfileirar();
            email.AdicionarTentativa(Base, sucesso: false, "SMTP_TIMEOUT");
            email.Enfileirar();
            email.AdicionarTentativa(Base.AddMinutes(5), sucesso: false, "SMTP_REJEITADO");
            _emails.Adicionar(email);

            var resultado = await _handler.HandleAsync(new ConsultarStatusQuery(email.Id), CancellationToken.None);

            Assert.NotNull(resultado);
            Assert.Equal(Base.AddMinutes(5), resultado.UltimaTentativaEm);
            Assert.Equal("SMTP_REJEITADO", resultado.UltimoErro);
        }

        [Fact]
        public async Task Handle_UltimaTentativaComFalha_RetornaCodigoDoErro()
        {
            var email = NovoEmail();
            email.Enfileirar();
            email.AdicionarTentativa(Base, sucesso: false, "SMTP_TIMEOUT");
            _emails.Adicionar(email);

            var resultado = await _handler.HandleAsync(new ConsultarStatusQuery(email.Id), CancellationToken.None);

            Assert.NotNull(resultado);
            Assert.Equal("Falhou", resultado.Status);
            Assert.Equal(Base, resultado.UltimaTentativaEm);
            Assert.Equal("SMTP_TIMEOUT", resultado.UltimoErro);
        }

        [Fact]
        public async Task Handle_UltimaTentativaComSucesso_RetornaErroNulo()
        {
            var email = NovoEmail();
            email.Enfileirar();
            email.AdicionarTentativa(Base, sucesso: false, "SMTP_TIMEOUT");
            email.Enfileirar();
            email.AdicionarTentativa(Base.AddMinutes(5), sucesso: true);
            _emails.Adicionar(email);

            var resultado = await _handler.HandleAsync(new ConsultarStatusQuery(email.Id), CancellationToken.None);

            Assert.NotNull(resultado);
            Assert.Equal("Enviado", resultado.Status);
            Assert.Equal(Base.AddMinutes(5), resultado.UltimaTentativaEm);
            Assert.Null(resultado.UltimoErro);
        }

        [Fact]
        public async Task Handle_IdInexistente_RetornaNulo()
        {
            var resultado = await _handler.HandleAsync(new ConsultarStatusQuery(Guid.NewGuid()), CancellationToken.None);

            Assert.Null(resultado);
        }

        [Fact]
        public void ContratoDaApi_TextosDeStatusSaoOsEsperados()
        {
            var esperado = new[] { "Pendente", "Enfileirado", "Enviado", "Falhou" };

            Assert.Equal(esperado, Enum.GetValues<EmailStatusEnum>().Select(s => s.ToString()));
        }
    }
}
