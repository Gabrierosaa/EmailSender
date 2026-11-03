using EmailSender.Domain.Entities;
using EmailSender.Infrastructure.Services;
using EmailSender.Infrastructure.Settings;
using Microsoft.Extensions.Options;
using MimeKit;

namespace EmailSender.Infrastructure.Tests
{
    public class SmtpEmailSenderTests
    {
        private static SmtpEmailSender CriarSender() => new(Options.Create(new SmtpSettings
        {
            Host = "localhost",
            Porta = 1025,
            RemetenteEndereco = "teste@localhost"
        }));

        private static (Email, LoteEnvio) CriarLote(string? replyTo = null, string? html = "<b>Oi</b>", string? texto = "Oi")
        {
            var lote = LoteEnvio.CriarUnitario("Assunto", "Remetente", "dest@exemplo.com", "Destino",
                replyTo: replyTo, corpoHtml: html, corpoTexto: texto);
            return (lote.Emails.Single(), lote);
        }

        [Fact]
        public void MontarMensagem_PreencheRemetenteDestinatarioEAssunto()
        {
            var (email, lote) = CriarLote();

            var mensagem = CriarSender().MontarMensagem(email, lote);

            var from = Assert.Single(mensagem.From.Mailboxes);
            Assert.Equal("Remetente", from.Name);
            Assert.Equal("teste@localhost", from.Address);
            var to = Assert.Single(mensagem.To.Mailboxes);
            Assert.Equal("Destino", to.Name);
            Assert.Equal("dest@exemplo.com", to.Address);
            Assert.Equal("Assunto", mensagem.Subject);
        }

        [Fact]
        public void MontarMensagem_PreencheCorpoHtmlETexto()
        {
            var (email, lote) = CriarLote();

            var mensagem = CriarSender().MontarMensagem(email, lote);

            Assert.Equal("<b>Oi</b>", mensagem.HtmlBody);
            Assert.Equal("Oi", mensagem.TextBody);
        }

        [Fact]
        public void MontarMensagem_SemReplyTo_NaoAdicionaReplyTo()
        {
            var (email, lote) = CriarLote();

            Assert.Empty(CriarSender().MontarMensagem(email, lote).ReplyTo);
        }

        [Fact]
        public void MontarMensagem_ComReplyTo_AdicionaReplyTo()
        {
            var (email, lote) = CriarLote(replyTo: "resposta@exemplo.com");

            var reply = Assert.Single(CriarSender().MontarMensagem(email, lote).ReplyTo.Mailboxes);
            Assert.Equal("resposta@exemplo.com", reply.Address);
        }

        [Fact]
        public void MontarMensagem_DerivaMessageIdDoEmailId()
        {
            var (email, lote) = CriarLote();

            Assert.Equal($"{email.Id}@emailsender", CriarSender().MontarMensagem(email, lote).MessageId);
        }
    }
}
