using EmailSender.Domain.Entities;
using EmailSender.Domain.Exceptions;

namespace EmailSender.Domain.Tests
{
    public class LoteEnvioTests
    {
        private static LoteEnvio CriarLote() =>
            new("Assunto", "Remetente", corpoTexto: "Corpo");

        private static Email CriarEmail(Guid loteId, string destinatario = "a@teste.com") =>
            new(loteId, destinatario, "Nome", "{}");

        [Fact]
        public void Construtor_Valido_DeveGuardarDadosInformados()
        {
            var lote = new LoteEnvio(
                "Assunto", "Remetente",
                idempotencyKey: "chave", requestHash: "hash", replyTo: "r@teste.com",
                corpoHtml: "<p>oi</p>", corpoTexto: "oi");

            Assert.NotEqual(Guid.Empty, lote.Id);
            Assert.Equal("Assunto", lote.Assunto);
            Assert.Equal("Remetente", lote.RemetenteNome);
            Assert.Equal("chave", lote.IdempotencyKey);
            Assert.Equal("hash", lote.RequestHash);
            Assert.Equal("r@teste.com", lote.ReplyTo);
            Assert.Equal("<p>oi</p>", lote.CorpoHtml);
            Assert.Equal("oi", lote.CorpoTexto);
            Assert.True(DateTimeOffset.UtcNow - lote.CriadoEm < TimeSpan.FromMinutes(1));
        }

        [Fact]
        public void Construtor_SomenteCorpoHtml_DeveSerValido()
        {
            var lote = new LoteEnvio("Assunto", "Remetente", corpoHtml: "<p>oi</p>");

            Assert.Equal("<p>oi</p>", lote.CorpoHtml);
        }

        [Fact]
        public void Construtor_DeveGerarIdsDiferentes()
        {
            Assert.NotEqual(CriarLote().Id, CriarLote().Id);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Construtor_AssuntoVazio_DeveLancarDomainException(string? assunto)
        {
            Assert.Throws<DomainException>(() => new LoteEnvio(assunto!, "Remetente", corpoTexto: "Corpo"));
        }

        [Fact]
        public void Construtor_AssuntoNoLimite_DeveSerValido()
        {
            var assunto = new string('a', LoteEnvio.AssuntoMaxLength);

            var lote = new LoteEnvio(assunto, "Remetente", corpoTexto: "Corpo");

            Assert.Equal(assunto, lote.Assunto);
        }

        [Fact]
        public void Construtor_AssuntoAcimaDoLimite_DeveLancarDomainException()
        {
            var assunto = new string('a', LoteEnvio.AssuntoMaxLength + 1);

            Assert.Throws<DomainException>(() => new LoteEnvio(assunto, "Remetente", corpoTexto: "Corpo"));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Construtor_RemetenteVazio_DeveLancarDomainException(string? remetente)
        {
            Assert.Throws<DomainException>(() => new LoteEnvio("Assunto", remetente!, corpoTexto: "Corpo"));
        }

        [Fact]
        public void Construtor_RemetenteNoLimite_DeveSerValido()
        {
            var remetente = new string('a', LoteEnvio.RemetenteNomeMaxLength);

            var lote = new LoteEnvio("Assunto", remetente, corpoTexto: "Corpo");

            Assert.Equal(remetente, lote.RemetenteNome);
        }

        [Fact]
        public void Construtor_RemetenteAcimaDoLimite_DeveLancarDomainException()
        {
            var remetente = new string('a', LoteEnvio.RemetenteNomeMaxLength + 1);

            Assert.Throws<DomainException>(() => new LoteEnvio("Assunto", remetente, corpoTexto: "Corpo"));
        }

        [Theory]
        [InlineData(null, null)]
        [InlineData("", "")]
        [InlineData("  ", "  ")]
        public void Construtor_SemCorpo_DeveLancarDomainException(string? html, string? texto)
        {
            Assert.Throws<DomainException>(() => new LoteEnvio("Assunto", "Remetente", corpoHtml: html, corpoTexto: texto));
        }

        [Fact]
        public void LoteNovo_DeveIniciarVazio()
        {
            var lote = CriarLote();

            Assert.Empty(lote.Emails);
            Assert.Equal(0, lote.TotalDestinatarios);
        }

        [Fact]
        public void AdicionarEmail_Valido_DeveVincularAoLote()
        {
            var lote = CriarLote();
            var email = CriarEmail(lote.Id);

            lote.AdicionarEmail(email);

            Assert.Single(lote.Emails);
            Assert.Contains(email, lote.Emails);
            Assert.Equal(1, lote.TotalDestinatarios);
        }

        [Fact]
        public void AdicionarEmail_Varios_DeveManterQuantidadeEOrdem()
        {
            var lote = CriarLote();
            var primeiro = CriarEmail(lote.Id, "a@teste.com");
            var segundo = CriarEmail(lote.Id, "b@teste.com");
            var terceiro = CriarEmail(lote.Id, "c@teste.com");

            lote.AdicionarEmail(primeiro);
            lote.AdicionarEmail(segundo);
            lote.AdicionarEmail(terceiro);

            Assert.Equal(3, lote.TotalDestinatarios);
            Assert.Equal(new[] { primeiro, segundo, terceiro }, lote.Emails);
        }

        [Fact]
        public void AdicionarEmail_Nulo_DeveLancarDomainException()
        {
            var lote = CriarLote();

            Assert.Throws<DomainException>(() => lote.AdicionarEmail(null!));
            Assert.Equal(0, lote.TotalDestinatarios);
        }

        [Fact]
        public void AdicionarEmail_Duplicado_DeveLancarDomainException()
        {
            var lote = CriarLote();
            var email = CriarEmail(lote.Id);
            lote.AdicionarEmail(email);

            Assert.Throws<DomainException>(() => lote.AdicionarEmail(email));
            Assert.Equal(1, lote.TotalDestinatarios);
        }

        [Fact]
        public void AdicionarEmail_DeOutroLote_DeveLancarDomainException()
        {
            var lote = CriarLote();
            var outroLote = CriarLote();
            var email = CriarEmail(outroLote.Id);

            Assert.Throws<DomainException>(() => lote.AdicionarEmail(email));
            Assert.Equal(0, lote.TotalDestinatarios);
        }

        [Fact]
        public void Emails_NaoPermiteModificacaoExterna()
        {
            var lote = CriarLote();

            var colecao = Assert.IsAssignableFrom<ICollection<Email>>(lote.Emails);

            Assert.Throws<NotSupportedException>(() => colecao.Add(CriarEmail(lote.Id)));
            Assert.Equal(0, lote.TotalDestinatarios);
        }

        [Fact]
        public void ValidarEnvio_LoteVazio_DeveLancarDomainException()
        {
            var lote = CriarLote();

            Assert.Throws<DomainException>(() => lote.ValidarEnvio());
        }

        [Fact]
        public void ValidarEnvio_LoteComEmail_NaoDeveLancar()
        {
            var lote = CriarLote();
            lote.AdicionarEmail(CriarEmail(lote.Id));

            var excecao = Record.Exception(() => lote.ValidarEnvio());

            Assert.Null(excecao);
        }
    }
}
