using EmailSender.Domain.Enum;
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
        public EmailStatusEnum Status { get; private set; }

        private readonly List<TentativaEnvio> _tentativas = new();
        public IReadOnlyCollection<TentativaEnvio> Tentativas => _tentativas.AsReadOnly();

        private Email() { }

        public Email(Guid loteId, string destinatario, string nomeDestinatario, string variavelJson)
        {
            Id = Guid.NewGuid();
            LoteId = loteId;
            Destinatario = destinatario;
            NomeDestinatario = nomeDestinatario;
            this.variavelJson = variavelJson;
            Status = EmailStatusEnum.Pendente;

            Validacao();
        }

        public void Enfileirar()
        {
            if (Status != EmailStatusEnum.Pendente && Status != EmailStatusEnum.Falhou)
                throw new DomainException($"Não é possível enfileirar um email com status {Status}.");

            Status = EmailStatusEnum.Enfileirado;
        }

        public TentativaEnvio AdicionarTentativa(DateTimeOffset iniciadaEm, bool sucesso, string? codigoErro = null)
        {
            if (Status != EmailStatusEnum.Enfileirado)
                throw new DomainException($"Só é possível registrar tentativa de um email enfileirado (status atual: {Status}).");

            var tentativa = new TentativaEnvio(Id, _tentativas.Count + 1, iniciadaEm, sucesso, codigoErro!);

            _tentativas.Add(tentativa);
            Status = sucesso ? EmailStatusEnum.Enviado : EmailStatusEnum.Falhou;

            return tentativa;
        }

        private void Validacao()
        {
            if(Id == Guid.Empty)          
                throw new DomainException("O ID do email é obrigatório.");

            if(string.IsNullOrWhiteSpace(Destinatario))          
                throw new DomainException("O destinatário do email é obrigatório.");

            if (!MailAddress.TryCreate(Destinatario, out _))
                throw new DomainException("O destinatário informado não é um e-mail válido.");
        }

    }
}
