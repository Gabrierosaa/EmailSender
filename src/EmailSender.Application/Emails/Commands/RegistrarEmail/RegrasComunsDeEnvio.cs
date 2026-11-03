using System.Linq.Expressions;
using EmailSender.Domain.Entities;
using EmailSender.Domain.Exceptions;
using EmailSender.Domain.ValueObjects;
using FluentValidation;

namespace EmailSender.Application.Emails.Commands.RegistrarEmail
{
    internal static class RegrasComunsDeEnvio
    {
        public const int NomeDestinatarioMaxLength = 200;
        public const int ReplyToMaxLength = EnderecoEmail.MaxLength;

        public static void AplicarRegrasDoLote<T>(
            AbstractValidator<T> validator,
            Expression<Func<T, string>> assunto,
            Expression<Func<T, string>> remetenteNome,
            Expression<Func<T, string?>> replyTo,
            Func<T, string?> corpoHtml,
            Func<T, string?> corpoTexto)
        {
            validator.RuleFor(assunto)
                .NotEmpty().WithMessage("O assunto é obrigatório.")
                .MaximumLength(LoteEnvio.AssuntoMaxLength)
                    .WithMessage($"O assunto não pode ultrapassar {LoteEnvio.AssuntoMaxLength} caracteres.")
                .Must(SemQuebraDeLinha).WithMessage("O assunto não pode conter quebra de linha.");

            validator.RuleFor(remetenteNome)
                .NotEmpty().WithMessage("O nome do remetente é obrigatório.")
                .MaximumLength(LoteEnvio.RemetenteNomeMaxLength)
                    .WithMessage($"O nome do remetente não pode ultrapassar {LoteEnvio.RemetenteNomeMaxLength} caracteres.")
                .Must(SemQuebraDeLinha).WithMessage("O nome do remetente não pode conter quebra de linha.");

            var replyToFunc = replyTo.Compile();
            validator.RuleFor(replyTo)
                .Cascade(CascadeMode.Stop)
                .MaximumLength(ReplyToMaxLength)
                    .WithMessage($"O endereço de resposta não pode ultrapassar {ReplyToMaxLength} caracteres.")
                .Must(SemQuebraDeLinha).WithMessage("O endereço de resposta não pode conter quebra de linha.")
                .Must(EmailValido).WithMessage("O endereço de resposta informado não é um e-mail válido.")
                .When(r => !string.IsNullOrWhiteSpace(replyToFunc(r)));

            validator.RuleFor(r => r)
                .Must(r => !string.IsNullOrWhiteSpace(corpoHtml(r)) || !string.IsNullOrWhiteSpace(corpoTexto(r)))
                .OverridePropertyName("Corpo")
                .WithMessage("Informe o corpo do e-mail em HTML ou em texto.");
        }

        public static bool SemQuebraDeLinha(string? valor) =>
            valor is null || !valor.Any(c => c is (char)13 or (char)10 or (char)0x85 or (char)0x2028 or (char)0x2029);

        public static bool EmailValido(string? valor)
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
