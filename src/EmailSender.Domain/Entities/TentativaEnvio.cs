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
    }
}
   