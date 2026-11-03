using EmailSender.Domain.Exceptions;
using System.Net.Mail;

namespace EmailSender.Domain.ValueObjects
{
    public sealed record EnderecoEmail
    {
        public const int MaxLength = 254;

        public string Valor { get; }

        private EnderecoEmail(string valor)
        {
            Valor = valor;
        }

        public static EnderecoEmail Criar(string? valor)
        {
            if (string.IsNullOrWhiteSpace(valor))
                throw new DomainException("O destinatário do email é obrigatório.");

            var normalizado = valor.Trim();

            if (normalizado.Length > MaxLength)
                throw new DomainException($"O e-mail não pode ultrapassar {MaxLength} caracteres.");

            if (!MailAddress.TryCreate(normalizado, out var endereco) || endereco.Address != normalizado)
                throw new DomainException("O destinatário informado não é um e-mail válido.");

            return new EnderecoEmail(normalizado);
        }

        public override string ToString() => Valor;
    }
}
