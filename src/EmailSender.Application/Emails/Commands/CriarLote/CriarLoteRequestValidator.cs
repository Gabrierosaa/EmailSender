using EmailSender.Application.DTOs;
using EmailSender.Application.Emails.Commands.RegistrarEmail;
using EmailSender.Domain.Entities;
using EmailSender.Domain.ValueObjects;
using FluentValidation;

namespace EmailSender.Application.Emails.Commands.CriarLote
{
    public sealed class CriarLoteRequestValidator : AbstractValidator<CriarLoteRequest>
    {
        public CriarLoteRequestValidator()
        {
            RegrasComunsDeEnvio.AplicarRegrasDoLote(
                this, r => r.Assunto, r => r.RemetenteNome, r => r.ReplyTo, r => r.CorpoHtml, r => r.CorpoTexto);

            RuleFor(r => r.Destinatarios)
                .Cascade(CascadeMode.Stop)
                .NotNull().WithMessage("Informe ao menos um destinatário.")
                .NotEmpty().WithMessage("Informe ao menos um destinatário.")
                .Must(d => d!.Count <= LoteEnvio.MaxDestinatarios)
                    .WithMessage($"Um lote não pode ter mais de {LoteEnvio.MaxDestinatarios} destinatários.");

            RuleForEach(r => r.Destinatarios).ChildRules(destinatario =>
            {
                destinatario.RuleFor(d => d.Email)
                    .Cascade(CascadeMode.Stop)
                    .NotEmpty().WithMessage("O destinatário é obrigatório.")
                    .MaximumLength(EnderecoEmail.MaxLength)
                        .WithMessage($"O destinatário não pode ultrapassar {EnderecoEmail.MaxLength} caracteres.")
                    .Must(RegrasComunsDeEnvio.SemQuebraDeLinha).WithMessage("O destinatário não pode conter quebra de linha.")
                    .Must(RegrasComunsDeEnvio.EmailValido).WithMessage("O destinatário informado não é um e-mail válido.");

                destinatario.RuleFor(d => d.Nome)
                    .MaximumLength(RegrasComunsDeEnvio.NomeDestinatarioMaxLength)
                        .WithMessage($"O nome do destinatário não pode ultrapassar {RegrasComunsDeEnvio.NomeDestinatarioMaxLength} caracteres.")
                    .Must(RegrasComunsDeEnvio.SemQuebraDeLinha).WithMessage("O nome do destinatário não pode conter quebra de linha.");

                destinatario.RuleFor(d => d.Variaveis)
                    .Must(v => v!.Keys.All(chave => !string.IsNullOrWhiteSpace(chave)))
                        .WithMessage("As chaves das variáveis não podem ser vazias.")
                    .When(d => d.Variaveis is not null);
            });

            RuleFor(r => r.Destinatarios).Custom((destinatarios, context) =>
            {
                if (destinatarios is null)
                    return;

                var vistos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                for (var i = 0; i < destinatarios.Count; i++)
                {
                    var email = destinatarios[i]?.Email?.Trim();
                    if (string.IsNullOrEmpty(email))
                        continue;

                    if (!vistos.Add(email))
                        context.AddFailure($"Destinatarios[{i}].Email", "Destinatário duplicado no lote.");
                }
            });
        }
    }
}
