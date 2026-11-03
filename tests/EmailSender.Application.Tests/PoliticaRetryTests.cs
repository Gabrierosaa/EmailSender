using EmailSender.Application.Abstractions;

namespace EmailSender.Application.Tests
{
    public class PoliticaRetryTests
    {
        private static readonly PoliticaRetry Politica =
            new(3, [TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(60)]);

        [Theory]
        [InlineData(1, true)]
        [InlineData(2, true)]
        [InlineData(3, false)]
        public void PodeTentarNovamente_SoEnquantoHouverTentativasRestantes(int feitas, bool esperado) =>
            Assert.Equal(esperado, Politica.PodeTentarNovamente(feitas));

        [Fact]
        public void IntervaloApos_UsaOIntervaloDaPosicao()
        {
            Assert.Equal(TimeSpan.FromSeconds(15), Politica.IntervaloApos(1));
            Assert.Equal(TimeSpan.FromSeconds(60), Politica.IntervaloApos(2));
        }

        [Fact]
        public void IntervaloApos_ListaCurta_RepeteOUltimoValor()
        {
            var politica = new PoliticaRetry(5, [TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(60)]);

            Assert.Equal(TimeSpan.FromSeconds(60), politica.IntervaloApos(3));
            Assert.Equal(TimeSpan.FromSeconds(60), politica.IntervaloApos(4));
        }

        [Fact]
        public void Construtor_ArgumentosInvalidos_Lanca()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new PoliticaRetry(0, [TimeSpan.FromSeconds(1)]));
            Assert.Throws<ArgumentException>(() => new PoliticaRetry(3, []));
            Assert.Throws<ArgumentNullException>(() => new PoliticaRetry(3, null!));
        }

        [Fact]
        public void IntervaloApos_Zero_Lanca() =>
            Assert.Throws<ArgumentOutOfRangeException>(() => Politica.IntervaloApos(0));
    }
}
