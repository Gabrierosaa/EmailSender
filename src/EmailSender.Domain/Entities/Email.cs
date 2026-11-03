using EmailSender.Domain.Enum;
using EmailSender.Domain.Exceptions;
using EmailSender.Domain.ValueObjects;

namespace EmailSender.Domain.Entities
{
    public class Email
    {
        public Guid Id { get; private set; }
        public Guid LoteId { get; private set; }
        public string Destinatario { get; private set; }
        public string NomeDestinatario { get; private set; }
        public string VariavelJson { get; private set; }
        public EmailStatusEnum Status { get; private set; }

        private readonly List<TentativaEnvio> _tentativas = new();
        public IReadOnlyCollection<TentativaEnvio> Tentativas => _tentativas.AsReadOnly();

        private Email() { }

        public Email(Guid loteId, string destinatario, string nomeDestinatario, string variavelJson)
        {
            Id = Guid.NewGuid();
            LoteId = loteId;
            Destinatario = EnderecoEmail.Criar(destinatario).Valor;
            NomeDestinatario = nomeDestinatario ?? string.Empty;
            VariavelJson = variavelJson ?? "{}";
            Status = EmailStatusEnum.Pendente;
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

        public TentativaEnvio RegistrarSucesso(DateTimeOffset iniciadaEm)
            => AdicionarTentativa(iniciadaEm, sucesso: true);

        public TentativaEnvio RegistrarFalha(DateTimeOffset iniciadaEm, string codigoErro)
            => AdicionarTentativa(iniciadaEm, sucesso: false, codigoErro);
    }
}
