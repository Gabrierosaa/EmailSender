namespace EmailSender.Domain.Entities
{
    public class Email
    {
        public Guid Id { get; private set; }
        public Guid LoteId { get; private set; }
        public string Destinatario { get; private set; }
        public string NomeDestinatario { get; private set; }
        public string variavelJson { get; private set; }
    }
}
