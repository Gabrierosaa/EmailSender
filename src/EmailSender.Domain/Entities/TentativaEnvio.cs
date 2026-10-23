using EmailSender.Domain.Exceptions;

namespace EmailSender.Domain.Entities
{
    public class TentativaEnvio
    {
        public Guid Id { get; set; }
        public Guid EmailId { get; set; }
        public int Numero { get; set; }
        public DateTimeOffset IniciadaEm { get; set; }
        public bool Sucesso { get; set; }
        public string CodigoErro { get; set; }

        private TentativaEnvio() { }

        public TentativaEnvio(Guid emailId, int numero, DateTimeOffset iniciadaEm, bool sucesso, string codigoErro)
        {
            Id = Guid.NewGuid();
            EmailId = emailId;
            Numero = numero;
            IniciadaEm = iniciadaEm;
            Sucesso = sucesso;
            CodigoErro = codigoErro;

            Validacao();
        }

        private void Validacao()
        {
            if (Id == Guid.Empty)
                throw new DomainException("O ID da tentativa de envio é obrigatório.");

            if (EmailId == Guid.Empty)
                throw new DomainException("O ID do email é obrigatório.");

            if (Numero <= 0)
                throw new DomainException("O número da tentativa de envio deve ser maior que zero.");

            if (IniciadaEm == default)
                throw new DomainException("A data de início da tentativa de envio é obrigatória.");

            if (!Sucesso && string.IsNullOrWhiteSpace(CodigoErro))
                throw new DomainException("Uma tentativa com falha deve ter código de erro.");

            if (Sucesso && !string.IsNullOrWhiteSpace(CodigoErro))
                throw new DomainException("Uma tentativa com sucesso não deve ter código de erro.");
        }

    }
}
   