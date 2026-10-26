using EmailSender.Domain.Entities;
using EmailSender.Domain.Exceptions;

namespace EmailSender.Domain.Tests
{
    public class TentativaEnvioTests
    {
        private static readonly DateTimeOffset Inicio = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);

        [Fact]
        public void Construtor_Sucesso_DeveGuardarDados()
        {
            var emailId = Guid.NewGuid();

            var tentativa = new TentativaEnvio(emailId, 1, Inicio, true, null!);

            Assert.NotEqual(Guid.Empty, tentativa.Id);
            Assert.Equal(emailId, tentativa.EmailId);
            Assert.Equal(1, tentativa.Numero);
            Assert.Equal(Inicio, tentativa.IniciadaEm);
            Assert.True(tentativa.Sucesso);
        }

        [Fact]
        public void Construtor_Falha_ComCodigoErro_DeveGuardarCodigo()
        {
            var tentativa = new TentativaEnvio(Guid.NewGuid(), 2, Inicio, false, "SMTP_TIMEOUT");

            Assert.False(tentativa.Sucesso);
            Assert.Equal("SMTP_TIMEOUT", tentativa.CodigoErro);
        }

        [Fact]
        public void Construtor_EmailIdVazio_DeveLancarDomainException()
        {
            Assert.Throws<DomainException>(() => new TentativaEnvio(Guid.Empty, 1, Inicio, true, null!));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void Construtor_NumeroNaoPositivo_DeveLancarDomainException(int numero)
        {
            Assert.Throws<DomainException>(() => new TentativaEnvio(Guid.NewGuid(), numero, Inicio, true, null!));
        }

        [Fact]
        public void Construtor_DataPadrao_DeveLancarDomainException()
        {
            Assert.Throws<DomainException>(() => new TentativaEnvio(Guid.NewGuid(), 1, default, true, null!));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Construtor_FalhaSemCodigoErro_DeveLancarDomainException(string? codigoErro)
        {
            Assert.Throws<DomainException>(() => new TentativaEnvio(Guid.NewGuid(), 1, Inicio, false, codigoErro!));
        }

        [Fact]
        public void Construtor_SucessoComCodigoErro_DeveLancarDomainException()
        {
            Assert.Throws<DomainException>(() => new TentativaEnvio(Guid.NewGuid(), 1, Inicio, true, "ERRO"));
        }
    }
}
