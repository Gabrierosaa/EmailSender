using EmailSender.Domain.Exceptions;

namespace EmailSender.Domain.Entities
{
    public class LoteEnvio
    {
        public Guid Id { get; private set; }
        public string? IdempotencyKey { get; private set; }
        public string? RequestHash { get; private set; }
        public string Assunto { get; private set; }
        public string RemetenteNome { get; private set; }
        public string? ReplyTo { get; private set; }
        public string? CorpoHtml { get; private set; }
        public string? CorpoTexto { get; private set; }
        public int TotalDestinatarios { get; private set; }
        public DateTimeOffset CriadoEm { get; private set; }

        private LoteEnvio() { }

        public LoteEnvio(
            string assunto,
            string remetenteNome,
            int totalDestinatarios,
            string? idempotencyKey = null,
            string? requestHash = null,
            string? replyTo = null,
            string? corpoHtml = null,
            string? corpoTexto = null)
        {
            Validacao();

            Id = Guid.NewGuid();
            IdempotencyKey = idempotencyKey;
            RequestHash = requestHash;
            Assunto = assunto;
            RemetenteNome = remetenteNome;
            ReplyTo = replyTo;
            CorpoHtml = corpoHtml;
            CorpoTexto = corpoTexto;
            TotalDestinatarios = totalDestinatarios;
            CriadoEm = DateTimeOffset.UtcNow;
        }

        public const int AssuntoMaxLength = 300;
        public const int RemetenteNomeMaxLength = 200;

        private void Validacao()
        {


            if (Id == Guid.Empty)
                throw new DomainException("O ID do lote de envio é obrigatório.");

            if (string.IsNullOrWhiteSpace(Assunto) || Assunto.Length > AssuntoMaxLength)
                throw new DomainException($"O Assunto não pode ser vazio ou ultrapassar {AssuntoMaxLength} caracteres.");

            if (string.IsNullOrWhiteSpace(RemetenteNome) || RemetenteNome.Length > RemetenteNomeMaxLength)
                throw new DomainException($"O Nome do remetente do lote de envio é obrigatório e não pode ultrapassar {RemetenteNomeMaxLength} caracteres.");

            if (TotalDestinatarios <= 0)
                throw new DomainException("O total de destinatários do lote de envio deve ser maior que zero.");

            if (string.IsNullOrWhiteSpace(CorpoHtml) && string.IsNullOrWhiteSpace(CorpoTexto))
                throw new DomainException("Informe CorpoHtml ou CorpoTexto.");
        }

    }
}
