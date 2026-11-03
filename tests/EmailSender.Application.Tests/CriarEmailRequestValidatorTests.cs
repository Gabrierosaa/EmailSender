using EmailSender.Application.DTOs;
using EmailSender.Application.Emails.Commands.RegistrarEmail;
using EmailSender.Domain.Entities;
using EmailSender.Domain.ValueObjects;

namespace EmailSender.Application.Tests
{
    public class CriarEmailRequestValidatorTests
    {
        private readonly CriarEmailRequestValidator _validator = new();

        private static CriarEmailRequest Valido(
            string destinatario = "a@teste.com",
            string assunto = "Assunto",
            string remetente = "Remetente",
            string? nome = "Fulano",
            string? replyTo = null,
            string? html = null,
            string? texto = "Corpo") =>
            new(destinatario, assunto, remetente, nome, replyTo, html, texto);

        private string[] Mensagens(CriarEmailRequest request) =>
            _validator.Validate(request).Errors.Select(e => e.ErrorMessage).ToArray();

        [Fact]
        public void RequisicaoValida_NaoDeveTerErros()
        {
            Assert.Empty(Mensagens(Valido()));
        }

        [Fact]
        public void ApenasCorpoHtml_DeveSerValido()
        {
            Assert.Empty(Mensagens(Valido(html: "<p>oi</p>", texto: null)));
        }

        [Fact]
        public void CamposOpcionaisAusentes_DeveSerValido()
        {
            Assert.Empty(Mensagens(Valido(nome: null, replyTo: null)));
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void Destinatario_Vazio_DeveRetornarMensagemEmPortugues(string destinatario)
        {
            Assert.Contains("O destinatário é obrigatório.", Mensagens(Valido(destinatario: destinatario)));
        }

        [Theory]
        [InlineData("sem-arroba")]
        [InlineData("@teste.com")]
        [InlineData("a@")]
        [InlineData("Fulano <a@teste.com>")]
        public void Destinatario_FormatoInvalido_DeveRetornarMensagem(string destinatario)
        {
            Assert.Contains("O destinatário informado não é um e-mail válido.", Mensagens(Valido(destinatario: destinatario)));
        }

        [Fact]
        public void Destinatario_AcimaDoLimite_DeveRetornarMensagem()
        {
            var destinatario = new string('a', EnderecoEmail.MaxLength) + "@teste.com";

            Assert.Contains(
                $"O destinatário não pode ultrapassar {EnderecoEmail.MaxLength} caracteres.",
                Mensagens(Valido(destinatario: destinatario)));
        }

        [Theory]
        [InlineData("a@teste.com\r\nBcc: x@teste.com")]
        [InlineData("a@teste.com\n")]
        [InlineData("a@teste.com\r")]
        public void Destinatario_ComQuebraDeLinha_DeveSerRejeitado(string destinatario)
        {
            Assert.Contains("O destinatário não pode conter quebra de linha.", Mensagens(Valido(destinatario: destinatario)));
        }

        [Theory]
        [InlineData("")]
        [InlineData("  ")]
        public void Assunto_Vazio_DeveRetornarMensagem(string assunto)
        {
            Assert.Contains("O assunto é obrigatório.", Mensagens(Valido(assunto: assunto)));
        }

        [Fact]
        public void Assunto_NoLimite_DeveSerValido_EAcimaDoLimiteNao()
        {
            Assert.Empty(Mensagens(Valido(assunto: new string('a', LoteEnvio.AssuntoMaxLength))));
            Assert.Contains(
                $"O assunto não pode ultrapassar {LoteEnvio.AssuntoMaxLength} caracteres.",
                Mensagens(Valido(assunto: new string('a', LoteEnvio.AssuntoMaxLength + 1))));
        }

        [Fact]
        public void Assunto_ComQuebraDeLinha_DeveSerRejeitado()
        {
            Assert.Contains("O assunto não pode conter quebra de linha.", Mensagens(Valido(assunto: "Oi\r\nBcc: x@teste.com")));
        }

        [Fact]
        public void Remetente_Vazio_DeveRetornarMensagem()
        {
            Assert.Contains("O nome do remetente é obrigatório.", Mensagens(Valido(remetente: "")));
        }

        [Fact]
        public void Remetente_AcimaDoLimite_DeveRetornarMensagem()
        {
            Assert.Contains(
                $"O nome do remetente não pode ultrapassar {LoteEnvio.RemetenteNomeMaxLength} caracteres.",
                Mensagens(Valido(remetente: new string('a', LoteEnvio.RemetenteNomeMaxLength + 1))));
        }

        [Fact]
        public void Remetente_ComQuebraDeLinha_DeveSerRejeitado()
        {
            Assert.Contains("O nome do remetente não pode conter quebra de linha.", Mensagens(Valido(remetente: "Ana\nBcc: x")));
        }

        [Fact]
        public void NomeDestinatario_AcimaDoLimite_DeveRetornarMensagem()
        {
            Assert.Contains(
                $"O nome do destinatário não pode ultrapassar {CriarEmailRequestValidator.NomeDestinatarioMaxLength} caracteres.",
                Mensagens(Valido(nome: new string('a', CriarEmailRequestValidator.NomeDestinatarioMaxLength + 1))));
        }

        [Fact]
        public void NomeDestinatario_ComQuebraDeLinha_DeveSerRejeitado()
        {
            Assert.Contains("O nome do destinatário não pode conter quebra de linha.", Mensagens(Valido(nome: "Fulano\r\nX: y")));
        }

        [Fact]
        public void ReplyTo_Invalido_DeveRetornarMensagem()
        {
            Assert.Contains("O endereço de resposta informado não é um e-mail válido.", Mensagens(Valido(replyTo: "invalido")));
        }

        [Fact]
        public void ReplyTo_ComQuebraDeLinha_DeveSerRejeitado()
        {
            Assert.Contains("O endereço de resposta não pode conter quebra de linha.", Mensagens(Valido(replyTo: "r@teste.com\r\nBcc: x")));
        }

        [Fact]
        public void ReplyTo_Valido_DeveSerAceito()
        {
            Assert.Empty(Mensagens(Valido(replyTo: "r@teste.com")));
        }

        [Theory]
        [InlineData(null, null)]
        [InlineData("", "")]
        [InlineData("  ", "  ")]
        public void SemCorpo_DeveRetornarMensagem(string? html, string? texto)
        {
            Assert.Contains("Informe o corpo do e-mail em HTML ou em texto.", Mensagens(Valido(html: html, texto: texto)));
        }

        [Fact]
        public void VariosErros_DevemSerRetornadosJuntos()
        {
            var mensagens = Mensagens(new CriarEmailRequest("", "", ""));

            Assert.Contains("O destinatário é obrigatório.", mensagens);
            Assert.Contains("O assunto é obrigatório.", mensagens);
            Assert.Contains("O nome do remetente é obrigatório.", mensagens);
            Assert.Contains("Informe o corpo do e-mail em HTML ou em texto.", mensagens);
        }
    }
}
