using EmailSender.Application.DTOs;
using EmailSender.Application.Emails.Commands.CriarLote;
using EmailSender.Domain.Entities;
using FluentValidation;

namespace EmailSender.Application.Tests
{
    public class CriarLoteRequestValidatorTests
    {
        private readonly CriarLoteRequestValidator _validator = new();

        private static CriarLoteRequest Valido(params string[] emails) =>
            new("Assunto", "Remetente", CorpoTexto: "Corpo",
                Destinatarios: (emails.Length == 0 ? new[] { "a@teste.com", "b@teste.com" } : emails)
                    .Select(e => new DestinatarioLoteRequest(e)).ToList());

        [Fact]
        public void Valido_NaoDeveTerErros()
        {
            Assert.True(_validator.Validate(Valido()).IsValid);
        }

        [Fact]
        public void ListaNula_DeveFalhar()
        {
            var resultado = _validator.Validate(Valido() with { Destinatarios = null });

            Assert.Contains(resultado.Errors, e => e.PropertyName == "Destinatarios");
        }

        [Fact]
        public void ListaVazia_DeveFalhar()
        {
            var resultado = _validator.Validate(Valido() with { Destinatarios = new List<DestinatarioLoteRequest>() });

            Assert.Contains(resultado.Errors, e => e.PropertyName == "Destinatarios");
        }

        [Fact]
        public void NoLimite_DeveSerValido()
        {
            var emails = Enumerable.Range(0, LoteEnvio.MaxDestinatarios).Select(i => $"u{i}@teste.com").ToArray();

            Assert.True(_validator.Validate(Valido(emails)).IsValid);
        }

        [Fact]
        public void AcimaDoLimite_DeveFalhar()
        {
            var emails = Enumerable.Range(0, LoteEnvio.MaxDestinatarios + 1).Select(i => $"u{i}@teste.com").ToArray();

            var resultado = _validator.Validate(Valido(emails));

            Assert.Contains(resultado.Errors, e => e.PropertyName == "Destinatarios");
        }

        [Fact]
        public void EmailInvalidoNoSegundoItem_DeveApontarOIndice()
        {
            var resultado = _validator.Validate(Valido("a@teste.com", "invalido", "c@teste.com"));

            var erro = Assert.Single(resultado.Errors);
            Assert.Equal("Destinatarios[1].Email", erro.PropertyName);
            Assert.Equal("O destinatário informado não é um e-mail válido.", erro.ErrorMessage);
        }

        [Fact]
        public void VariosErros_DevemSairJuntos()
        {
            var request = new CriarLoteRequest("", "", Destinatarios: new List<DestinatarioLoteRequest>
            {
                new("invalido"),
                new("ok@teste.com", new string('n', 201)),
                new("ok@teste.com", Variaveis: new Dictionary<string, string> { [" "] = "x" })
            });

            var propriedades = _validator.Validate(request).Errors.Select(e => e.PropertyName).ToList();

            Assert.Contains("Assunto", propriedades);
            Assert.Contains("RemetenteNome", propriedades);
            Assert.Contains("Corpo", propriedades);
            Assert.Contains("Destinatarios[0].Email", propriedades);
            Assert.Contains("Destinatarios[1].Nome", propriedades);
            Assert.Contains("Destinatarios[2].Variaveis", propriedades);
            Assert.Contains("Destinatarios[2].Email", propriedades);
        }

        [Fact]
        public void Duplicado_IgnorandoMaiusculasEEspacos_MarcaApartirDaSegundaOcorrencia()
        {
            var resultado = _validator.Validate(Valido("a@teste.com", "b@teste.com", "A@TESTE.COM", " b@teste.com"));

            var duplicados = resultado.Errors.Where(e => e.ErrorMessage == "Destinatário duplicado no lote.").ToList();
            Assert.Equal(new[] { "Destinatarios[2].Email", "Destinatarios[3].Email" }, duplicados.Select(e => e.PropertyName));
        }

        [Fact]
        public void AssuntoVazio_DeveFalhar()
        {
            var resultado = _validator.Validate(Valido() with { Assunto = "" });

            Assert.Contains(resultado.Errors, e => e.PropertyName == "Assunto");
        }

        [Fact]
        public void CorpoAusente_DeveFalhar()
        {
            var resultado = _validator.Validate(Valido() with { CorpoTexto = null, CorpoHtml = null });

            Assert.Contains(resultado.Errors, e => e.PropertyName == "Corpo");
        }

        [Fact]
        public void ReplyToInvalido_DeveFalhar()
        {
            var resultado = _validator.Validate(Valido() with { ReplyTo = "invalido" });

            Assert.Contains(resultado.Errors, e => e.PropertyName == "ReplyTo");
        }
    }
}
