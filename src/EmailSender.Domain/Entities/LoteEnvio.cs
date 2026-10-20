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
            if (string.IsNullOrWhiteSpace(assunto))
                throw new ArgumentException("Assunto é obrigatório.");

            if (string.IsNullOrWhiteSpace(remetenteNome))
                throw new ArgumentException("Nome do remetente é obrigatório.");

            if (totalDestinatarios <= 0)
                throw new ArgumentException("O lote deve possuir pelo menos um destinatário.");

            Id = Guid.NewGuid();

            Assunto = assunto;
            RemetenteNome = remetenteNome;
            TotalDestinatarios = totalDestinatarios;

            IdempotencyKey = idempotencyKey;
            RequestHash = requestHash;
            ReplyTo = replyTo;
            CorpoHtml = corpoHtml;
            CorpoTexto = corpoTexto;

            CriadoEm = DateTimeOffset.UtcNow;
        }
    }
}
