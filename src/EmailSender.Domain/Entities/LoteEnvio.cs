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
        public DateTimeOffset CriadoEm { get; private set; }

        private readonly List<Email> _emails = new();
        public IReadOnlyCollection<Email> Emails => _emails.AsReadOnly();
        public int TotalDestinatarios => _emails.Count;

        private LoteEnvio() { }

        public LoteEnvio(
            string assunto,
            string remetenteNome,
            string? idempotencyKey = null,
            string? requestHash = null,
            string? replyTo = null,
            string? corpoHtml = null,
            string? corpoTexto = null)
        {
            Id = Guid.NewGuid();
            IdempotencyKey = idempotencyKey;
            RequestHash = requestHash;
            Assunto = assunto;
            RemetenteNome = remetenteNome;
            ReplyTo = replyTo;
            CorpoHtml = corpoHtml;
            CorpoTexto = corpoTexto;
            CriadoEm = DateTimeOffset.UtcNow;

            Validacao();
        }

        public void AdicionarEmail(Email email)
        {
            if (email is null)
                throw new DomainException("O email é obrigatório.");

            if (email.LoteId != Id)
                throw new DomainException("O email pertence a outro lote de envio.");

            if (_emails.Any(e => e.Id == email.Id))
                throw new DomainException("O email já foi adicionado a este lote de envio.");

            _emails.Add(email);
        }

        public void ValidarEnvio()
        {
            if (_emails.Count == 0)
                throw new DomainException("Um lote de envio vazio não pode ser enviado.");
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

            if (string.IsNullOrWhiteSpace(CorpoHtml) && string.IsNullOrWhiteSpace(CorpoTexto))
                throw new DomainException("Informe CorpoHtml ou CorpoTexto.");
        }

    }
}
