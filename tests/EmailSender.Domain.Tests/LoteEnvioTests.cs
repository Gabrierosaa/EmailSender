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
