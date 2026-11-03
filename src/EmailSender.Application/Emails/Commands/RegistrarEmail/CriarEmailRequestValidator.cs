using EmailSender.Application.DTOs;
using EmailSender.Domain.Entities;
using EmailSender.Domain.Exceptions;
using EmailSender.Domain.ValueObjects;
using FluentValidation;

namespace EmailSender.Application.Emails.Commands.RegistrarEmail
{
    public sealed class CriarEmailRequestValidator : AbstractValidator<CriarEmailRequest>
    {
        public const int NomeDestinatarioMaxLength = 200;
        public const int ReplyToMaxLength = EnderecoEmail.MaxLength;

        public CriarEmailRequestValidator()
        {
            RuleFor(r => r.Destinatario)
                .Cascade(CascadeMode.Stop)
                .NotEmpty().WithMessage("O destinatário é obrigatório.")
                .MaximumLength(EnderecoEmail.MaxLength)
                    .WithMessage($"O destinatário não pode ultrapassar {EnderecoEmail.MaxLength} caracteres.")
                .Must(SemQuebraDeLinha).WithMessage("O destinatário não pode conter quebra de linha.")
                .Must(EmailValido).WithMessage("O destinatário informado não é um e-mail válido.");

            RuleFor(r => r.NomeDestinatario)
                .MaximumLength(NomeDestinatarioMaxLength)
                    .WithMessage($"O nome do destinatário não pode ultrapassar {NomeDestinatarioMaxLength} caracteres.")
                .Must(SemQuebraDeLinha).WithMessage("O nome do destinatário não pode conter quebra de linha.");

            RuleFor(r => r.Assunto)
                .NotEmpty().WithMessage("O assunto é obrigatório.")
                .MaximumLength(LoteEnvio.AssuntoMaxLength)
                    .WithMessage($"O assunto não pode ultrapassar {LoteEnvio.AssuntoMaxLength} caracteres.")
                .Must(SemQuebraDeLinha).WithMessage("O assunto não pode conter quebra de linha.");

            RuleFor(r => r.RemetenteNome)
                .NotEmpty().WithMessage("O nome do remetente é obrigatório.")
                .MaximumLength(LoteEnvio.RemetenteNomeMaxLength)
                    .WithMessage($"O nome do remetente não pode ultrapassar {LoteEnvio.RemetenteNomeMaxLength} caracteres.")
                .Must(SemQuebraDeLinha).WithMessage("O nome do remetente não pode conter quebra de linha.");

            RuleFor(r => r.ReplyTo)
                .Cascade(CascadeMode.Stop)
                .MaximumLength(ReplyToMaxLength)
                    .WithMessage($"O endereço de resposta não pode ultrapassar {ReplyToMaxLength} caracteres.")
                .Must(SemQuebraDeLinha).WithMessage("O endereço de resposta não pode conter quebra de linha.")
                .Must(EmailValido).WithMessage("O endereço de resposta informado não é um e-mail válido.")
                .When(r => !string.IsNullOrWhiteSpace(r.ReplyTo));

            RuleFor(r => r)
                .Must(r => !string.IsNullOrWhiteSpace(r.CorpoHtml) || !string.IsNullOrWhiteSpace(r.CorpoTexto))
                .OverridePropertyName("Corpo")
                .WithMessage("Informe o corpo do e-mail em HTML ou em texto.");
        }

        private static bool SemQuebraDeLinha(string? valor) =>
            valor is null || !valor.Any(c => c is (char)13 or (char)10 or (char)0x85 or (char)0x2028 or (char)0x2029);

        private static bool EmailValido(string? valor)
        {
            try
            {
                EnderecoEmail.Criar(valor);
                return true;
            }
            catch (DomainException)
            {
                return false;
            }
        }
    }
}
