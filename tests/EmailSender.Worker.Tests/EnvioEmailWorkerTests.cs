using System.ComponentModel.DataAnnotations;
using EmailSender.Application.Emails.Commands.ProcessarEmailsPendentes;
using EmailSender.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace EmailSender.Worker.Tests
{
    public class EnvioEmailWorkerTests
    {
        private sealed class ProcessadorFake(Func<int, CancellationToken, Task<ResumoProcessamento>> acao) : IProcessadorEmailsPendentes
        {
            private int _chamadas;
            public int Chamadas => _chamadas;
            public List<DateTime> Momentos { get; } = [];
            public int UltimoLimite { get; private set; }

            public Task<ResumoProcessamento> HandleAsync(ProcessarEmailsPendentesCommand command, CancellationToken ct)
            {
                lock (Momentos) Momentos.Add(DateTime.UtcNow);
                UltimoLimite = command.Limite;
                return acao(Interlocked.Increment(ref _chamadas), ct);
            }
        }

        private static readonly ResumoProcessamento Vazio = new(0, 0, 0, 0, 0);

        private static EnvioEmailWorker Criar(IProcessadorEmailsPendentes p, int intervalo = 1, int lote = 50) =>
            new(p, Options.Create(new EnvioEmailWorkerSettings { IntervaloSegundos = intervalo, LimiteLote = lote }),
                NullLogger<EnvioEmailWorker>.Instance);

        private static async Task<bool> AguardarAsync(Func<bool> condicao, int timeoutMs = 5000)
        {
            var limite = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            while (DateTime.UtcNow < limite)
            {
                if (condicao()) return true;
                await Task.Delay(20);
            }
            return condicao();
        }

        [Fact]
        public async Task ExecutaPeloMenosDoisCiclos_ERespeitaIntervaloELote()
        {
            var p = new ProcessadorFake((_, _) => Task.FromResult(Vazio));
            var worker = Criar(p, intervalo: 1, lote: 7);

            await worker.StartAsync(CancellationToken.None);
            Assert.True(await AguardarAsync(() => p.Chamadas >= 2));
            await worker.StopAsync(CancellationToken.None);

            Assert.Equal(7, p.UltimoLimite);
            Assert.True((p.Momentos[1] - p.Momentos[0]).TotalMilliseconds >= 500);
        }

        [Fact]
        public async Task ExcecaoNoCiclo_NaoDerrubaOWorker()
        {
            var p = new ProcessadorFake((n, _) => n == 1
                ? throw new InvalidOperationException("falha")
                : Task.FromResult(Vazio));
            var worker = Criar(p);

            await worker.StartAsync(CancellationToken.None);
            Assert.True(await AguardarAsync(() => p.Chamadas >= 2));
            await worker.StopAsync(CancellationToken.None);

            Assert.Null(worker.ExecuteTask?.Exception);
        }

        [Fact]
        public async Task CancelamentoDuranteDelay_EncerraSemExcecao()
        {
            var p = new ProcessadorFake((_, _) => Task.FromResult(Vazio));
            var worker = Criar(p, intervalo: 3600);

            await worker.StartAsync(CancellationToken.None);
            Assert.True(await AguardarAsync(() => p.Chamadas >= 1));

            var inicio = DateTime.UtcNow;
            await worker.StopAsync(CancellationToken.None);

            Assert.True((DateTime.UtcNow - inicio).TotalSeconds < 1);
            Assert.True(worker.ExecuteTask!.IsCompletedSuccessfully);
        }

        [Fact]
        public async Task CancelamentoDuranteCiclo_PropagaTokenEEncerraSemExcecao()
        {
            var iniciou = new TaskCompletionSource();
            var p = new ProcessadorFake(async (_, ct) =>
            {
                iniciou.TrySetResult();
                await Task.Delay(Timeout.Infinite, ct);
                return Vazio;
            });
            var worker = Criar(p);

            await worker.StartAsync(CancellationToken.None);
            await iniciou.Task.WaitAsync(TimeSpan.FromSeconds(5));

            var inicio = DateTime.UtcNow;
            await worker.StopAsync(CancellationToken.None);

            Assert.True((DateTime.UtcNow - inicio).TotalSeconds < 1);
            Assert.True(worker.ExecuteTask!.IsCompletedSuccessfully);
            Assert.Equal(1, p.Chamadas);
        }
    }

    public class EnvioEmailWorkerSettingsTests
    {
        private static List<ValidationResult> Validar(EnvioEmailWorkerSettings s)
        {
            var r = new List<ValidationResult>();
            Validator.TryValidateObject(s, new ValidationContext(s), r, validateAllProperties: true);
            return r;
        }

        [Fact]
        public void Padrao_Eh10SegundosELote50_EValido()
        {
            var s = new EnvioEmailWorkerSettings();

            Assert.Equal(10, s.IntervaloSegundos);
            Assert.Equal(50, s.LimiteLote);
            Assert.Empty(Validar(s));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(3601)]
        public void IntervaloForaDaFaixa_Falha(int valor) =>
            Assert.NotEmpty(Validar(new EnvioEmailWorkerSettings { IntervaloSegundos = valor }));

        [Theory]
        [InlineData(0)]
        [InlineData(1001)]
        public void LimiteForaDaFaixa_Falha(int valor) =>
            Assert.NotEmpty(Validar(new EnvioEmailWorkerSettings { LimiteLote = valor }));
    }
}

namespace EmailSender.Worker.Tests
{
    public class RetrySettingsTests
    {
        private static List<string> Erros(RetrySettings s)
        {
            var r = new List<ValidationResult>();
            Validator.TryValidateObject(s, new ValidationContext(s), r, validateAllProperties: true);
            var erros = r.Select(x => x.ErrorMessage!).ToList();

            var custom = new RetrySettingsValidator().Validate(null, s);
            if (custom.Failed) erros.AddRange(custom.Failures!);
            return erros;
        }

        [Fact]
        public void Padrao_Eh3Tentativas_EListaDeIntervalosVemDaConfiguracao()
        {
            var s = new RetrySettings();

            Assert.Equal(3, s.MaxTentativas);
            Assert.Empty(s.IntervalosSegundos);
            Assert.NotEmpty(Erros(s));
        }

        [Fact]
        public void BindDoAppsettings_NaoDuplicaOsIntervalos()
        {
            var config = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Retry:MaxTentativas"] = "3",
                    ["Retry:IntervalosSegundos:0"] = "15",
                    ["Retry:IntervalosSegundos:1"] = "60",
                }).Build();

            var s = config.GetSection(RetrySettings.Secao).Get<RetrySettings>()!;

            Assert.Equal([15, 60], s.IntervalosSegundos);
            Assert.Empty(Erros(s));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(11)]
        public void MaxTentativasForaDaFaixa_Falha(int valor) =>
            Assert.NotEmpty(Erros(new RetrySettings { MaxTentativas = valor }));

        [Fact]
        public void ListaVazia_OuNula_Falha()
        {
            Assert.NotEmpty(Erros(new RetrySettings { IntervalosSegundos = null! }));
            ListaVazia_Falha();
        }

        private static void ListaVazia_Falha() =>
            Assert.NotEmpty(Erros(new RetrySettings { IntervalosSegundos = [] }));

        [Theory]
        [InlineData(60, 15)]
        [InlineData(15, 15)]
        public void ListaNaoEstritamenteCrescente_Falha(int a, int b) =>
            Assert.NotEmpty(Erros(new RetrySettings { IntervalosSegundos = [a, b] }));

        [Fact]
        public void IntervaloNaoPositivo_Falha() =>
            Assert.NotEmpty(Erros(new RetrySettings { IntervalosSegundos = [0, 10] }));
    }
}
