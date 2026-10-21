using EmailSender.Domain.Exceptions;
using System.Net.Mail;

namespace EmailSender.Domain.Entities
{
    public class Email
    {
        public Guid Id { get; private set; }
        public Guid LoteId { get; private set; }
        public string Destinatario { get; private set; }
        public string NomeDestinatario { get; private set; }
        public string variavelJson { get; private set; }
        
        private Email() { }

        public Email(Guid loteId, string destinatario, string nomeDestinatario, string variavelJson)
        {
            Validacao();

            Id = Guid.NewGuid();
            LoteId = loteId;
            Destinatario = destinatario;
            NomeDestinatario = nomeDestinatario;
            this.variavelJson = variavelJson;
        }

        private void Validacao()
        {
            if(Id == Guid.Empty)          
                throw new ArgumentException("O ID do email é obrigatório.");

            if(string.IsNullOrWhiteSpace(Destinatario))          
                throw new ArgumentException("O destinatário do email é obrigatório.");

            if (!MailAddress.TryCreate(Destinatario, out _))
                throw new ArgumentException("O destinatário informado não é um e-mail válido.");
        }

    }
}
