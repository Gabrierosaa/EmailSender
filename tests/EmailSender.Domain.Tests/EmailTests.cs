using EmailSender.Domain.Entities;
using EmailSender.Domain.Enum;
using EmailSender.Domain.Exceptions;

namespace EmailSender.Domain.Tests
{
    public class EmailTests
    {
        private static Email CriarEmail(string destinatario = "a@teste.com") =>
            new(Guid.NewGuid(), destinatario, "Nome", "{}");

        private static Email CriarEmailEnfileirado()
        {
            var email = CriarEmail();
            email.Enfileirar();
            return email;
        }

        [Fact]
        public void Construtor_Valido_DeveIniciarPendenteComDadosInformados()
        {
            var loteId = Guid.NewGuid();

            var email = new Email(loteId, "a@teste.com", "Fulano", "{\"x\":1}");

            Assert.NotEqual(Guid.Empty, email.Id);
            Assert.Equal(loteId, email.LoteId);
            Assert.Equal("a@teste.com", email.Destinatario);
            Assert.Equal("Fulano", email.NomeDestinatario);
            Assert.Equal("{\"x\":1}", email.variavelJson);
            Assert.Equal(EmailStatusEnum.Pendente, email.Status);
            Assert.Empty(email.Tentativas);
        }

        [Fact]
        public void Construtor_DeveGerarIdsDiferentes()
        {
            Assert.NotEqual(CriarEmail().Id, CriarEmail().Id);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Construtor_DestinatarioVazio_DeveLancarDomainException(string? destinatario)
        {
            Assert.Throws<DomainException>(() => CriarEmail(destinatario!));
        }

        [Theory]
        [InlineData("sem-arroba")]
        [InlineData("@teste.com")]
        [InlineData("a@")]
        public void Construtor_DestinatarioInvalido_DeveLancarDomainException(string destinatario)
        {
            Assert.Throws<DomainException>(() => CriarEmail(destinatario));
        }

        [Fact]
        public void Enfileirar_Pendente_DeveMudarParaEnfileirado()
        {
            var email = CriarEmail();

            email.Enfileirar();

            Assert.Equal(EmailStatusEnum.Enfileirado, email.Status);
        }

        [Fact]
        public void Enfileirar_Falhou_DevePermitirNovaTentativa()
        {
            var email = CriarEmailEnfileirado();
            email.AdicionarTentativa(DateTimeOffset.UtcNow, false, "ERRO");

            email.Enfileirar();

            Assert.Equal(EmailStatusEnum.Enfileirado, email.Status);
        }

        [Fact]
        public void Enfileirar_JaEnfileirado_DeveLancarDomainException()
        {
            var email = CriarEmailEnfileirado();

            Assert.Throws<DomainException>(() => email.Enfileirar());
            Assert.Equal(EmailStatusEnum.Enfileirado, email.Status);
        }

        [Fact]
        public void Enfileirar_Enviado_DeveLancarDomainException()
        {
            var email = CriarEmailEnfileirado();
            email.AdicionarTentativa(DateTimeOffset.UtcNow, true);

            Assert.Throws<DomainException>(() => email.Enfileirar());
            Assert.Equal(EmailStatusEnum.Enviado, email.Status);
        }

        [Fact]
        public void AdicionarTentativa_Sucesso_DeveMarcarComoEnviado()
        {
            var email = CriarEmailEnfileirado();
            var inicio = DateTimeOffset.UtcNow;

            var tentativa = email.AdicionarTentativa(inicio, true);

            Assert.Equal(EmailStatusEnum.Enviado, email.Status);
            Assert.Single(email.Tentativas);
            Assert.Same(tentativa, email.Tentativas.Single());
            Assert.Equal(email.Id, tentativa.EmailId);
            Assert.Equal(1, tentativa.Numero);
            Assert.Equal(inicio, tentativa.IniciadaEm);
            Assert.True(tentativa.Sucesso);
        }

        [Fact]
        public void AdicionarTentativa_Falha_DeveMarcarComoFalhou()
        {
            var email = CriarEmailEnfileirado();

            var tentativa = email.AdicionarTentativa(DateTimeOffset.UtcNow, false, "SMTP_TIMEOUT");

            Assert.Equal(EmailStatusEnum.Falhou, email.Status);
            Assert.False(tentativa.Sucesso);
            Assert.Equal("SMTP_TIMEOUT", tentativa.CodigoErro);
        }

        [Fact]
        public void AdicionarTentativa_Falha_SemCodigoErro_DeveLancarEManterStatus()
        {
            var email = CriarEmailEnfileirado();

            Assert.Throws<DomainException>(() => email.AdicionarTentativa(DateTimeOffset.UtcNow, false));

            Assert.Equal(EmailStatusEnum.Enfileirado, email.Status);
            Assert.Empty(email.Tentativas);
        }

        [Fact]
        public void AdicionarTentativa_VariasTentativas_DeveNumerarSequencialmente()
        {
            var email = CriarEmailEnfileirado();
            email.AdicionarTentativa(DateTimeOffset.UtcNow, false, "ERRO_1");
            email.Enfileirar();
            email.AdicionarTentativa(DateTimeOffset.UtcNow, false, "ERRO_2");
            email.Enfileirar();

            var terceira = email.AdicionarTentativa(DateTimeOffset.UtcNow, true);

            Assert.Equal(3, terceira.Numero);
            Assert.Equal(new[] { 1, 2, 3 }, email.Tentativas.Select(t => t.Numero));
            Assert.Equal(EmailStatusEnum.Enviado, email.Status);
        }

        [Fact]
        public void AdicionarTentativa_Pendente_DeveLancarDomainException()
        {
            var email = CriarEmail();

            Assert.Throws<DomainException>(() => email.AdicionarTentativa(DateTimeOffset.UtcNow, true));
            Assert.Empty(email.Tentativas);
        }

        [Fact]
        public void AdicionarTentativa_JaEnviado_DeveLancarDomainException()
        {
            var email = CriarEmailEnfileirado();
            email.AdicionarTentativa(DateTimeOffset.UtcNow, true);

            Assert.Throws<DomainException>(() => email.AdicionarTentativa(DateTimeOffset.UtcNow, true));
            Assert.Single(email.Tentativas);
        }

        [Fact]
        public void AdicionarTentativa_DataPadrao_DeveLancarDomainException()
        {
            var email = CriarEmailEnfileirado();

            Assert.Throws<DomainException>(() => email.AdicionarTentativa(default, true));
            Assert.Equal(EmailStatusEnum.Enfileirado, email.Status);
        }

        [Fact]
        public void Tentativas_NaoPermiteModificacaoExterna()
        {
            var email = CriarEmailEnfileirado();
            var colecao = Assert.IsAssignableFrom<ICollection<TentativaEnvio>>(email.Tentativas);

            Assert.Throws<NotSupportedException>(() =>
                colecao.Add(new TentativaEnvio(email.Id, 1, DateTimeOffset.UtcNow, true, null!)));
        }
    }
}
