using EmailSender.Application.DTOs;
using EmailSender.Domain.ValueObjects;
using FluentValidation;

namespace EmailSender.Application.Emails.Commands.RegistrarEmail
{
    public sealed class CriarEmailRequestValidator : AbstractValidator<CriarEmailRequest>
    {
        public const int NomeDestinatarioMaxLength = RegrasComunsDeEnvio.NomeDestinatarioMaxLength;
        public const int ReplyToMaxLength = RegrasComunsDeEnvio.ReplyToMaxLength;

        public CriarEmailRequestValidator()
        {
            RuleFor(r => r.Destinatario)
                .Cascade(CascadeMode.Stop)
                .NotEmpty().WithMessage("O destinatário é obrigatório.")
                .MaximumLength(EnderecoEmail.MaxLength)
                    .WithMessage($"O destinatário não pode ultrapassar {EnderecoEmail.MaxLength} caracteres.")
                .Must(RegrasComunsDeEnvio.SemQuebraDeLinha).WithMessage("O destinatário não pode conter quebra de linha.")
                .Must(RegrasComunsDeEnvio.EmailValido).WithMessage("O destinatário informado não é um e-mail válido.");

            RuleFor(r => r.NomeDestinatario)
                .MaximumLength(NomeDestinatarioMaxLength)
                    .WithMessage($"O nome do destinatário não pode ultrapassar {NomeDestinatarioMaxLength} caracteres.")
                .Must(RegrasComunsDeEnvio.SemQuebraDeLinha).WithMessage("O nome do destinatário não pode conter quebra de linha.");

            RegrasComunsDeEnvio.AplicarRegrasDoLote(
                this, r => r.Assunto, r => r.RemetenteNome, r => r.ReplyTo, r => r.CorpoHtml, r => r.CorpoTexto);
        }
    }
}
