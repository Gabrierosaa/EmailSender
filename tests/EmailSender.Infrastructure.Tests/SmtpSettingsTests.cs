using System.ComponentModel.DataAnnotations;
using EmailSender.Infrastructure.Settings;

namespace EmailSender.Infrastructure.Tests
{
    public class SmtpSettingsTests
    {
        private static SmtpSettings Valida() => new()
        {
            Host = "localhost",
            Porta = 1025,
            RemetenteEndereco = "teste@localhost"
        };

        private static List<ValidationResult> Validar(SmtpSettings settings)
        {
            var resultados = new List<ValidationResult>();
            Validator.TryValidateObject(settings, new ValidationContext(settings), resultados, validateAllProperties: true);
            return resultados;
        }

        [Fact]
        public void ConfiguracaoValida_Passa()
        {
            Assert.Empty(Validar(Valida()));
        }

        [Fact]
        public void HostVazio_Falha()
        {
            var settings = Valida();
            settings.Host = "";

            Assert.Contains(Validar(settings), r => r.MemberNames.Contains(nameof(SmtpSettings.Host)));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(70000)]
        public void PortaForaDoIntervalo_Falha(int porta)
        {
            var settings = Valida();
            settings.Porta = porta;

            Assert.Contains(Validar(settings), r => r.MemberNames.Contains(nameof(SmtpSettings.Porta)));
        }
    }
}
