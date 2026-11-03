using System.Collections.Concurrent;
using EmailSender.Application.Abstractions;
using EmailSender.Application.Emails.Commands.ProcessarEmailsPendentes;
using EmailSender.Domain.Entities;
using EmailSender.Domain.Enum;
using EmailSender.Domain.Repositories;
using EmailSender.Infrastructure.Persistence;
using EmailSender.Infrastructure.Persistence.Repositories;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace EmailSender.IntegrationTests
{
    [Collection(SqlServerCollection.Nome)]
    public class ProcessarEmailsPendentesTests : IAsyncLifetime
    {
        private readonly string _database = $"EmailSenderTests_{Guid.NewGuid():N}";
        private readonly CancellationToken _ct = CancellationToken.None;
        private readonly string _connectionString;
        private readonly FakeEmailSender _sender = new();

        public ProcessarEmailsPendentesTests(SqlServerFixture sql)
        {
            _connectionString = new SqlConnectionStringBuilder(sql.ConnectionString) { InitialCatalog = _database }.ConnectionString;
        }

        private ServiceProvider CriarProvider(TimeProvider? clock = null, PoliticaRetry? retry = null)
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddDbContext<AppDbContext>(o => o.UseSqlServer(_connectionString));
            services.AddScoped<IEmailRepository, EmailRepository>();
            services.AddScoped<ILoteEnvioRepository, LoteEnvioRepository>();
            services.AddScoped<IUnitOfWork, UnitOfWork>();
            services.AddSingleton<IEmailSender>(_sender);
            services.AddSingleton(clock ?? TimeProvider.System);
            services.AddSingleton(retry ?? PoliticaRetry.Padrao);
            services.AddScoped<ProcessarEmailHandler>();
            services.AddSingleton<ProcessarEmailsPendentesHandler>();
            return services.BuildServiceProvider();
        }

        private AppDbContext CriarContexto() =>
            new(new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(_connectionString).Options);

        public async Task InitializeAsync()
        {
            await using var context = CriarContexto();
            await context.Database.MigrateAsync();
        }

        public async Task DisposeAsync()
        {
            await using var context = CriarContexto();
            await context.Database.EnsureDeletedAsync();
        }

        private async Task<List<Email>> CriarPendentesAsync(int quantidade)
        {
            var lote = new LoteEnvio("Assunto", "Remetente", corpoTexto: "Corpo");
            var emails = Enumerable.Range(0, quantidade)
                .Select(i => new Email(lote.Id, $"p{i}@teste.com", "Nome", "{}"))
                .ToList();
            foreach (var e in emails)
                lote.AdicionarEmail(e);

            await using var context = CriarContexto();
            await new LoteEnvioRepository(context).AdicionarAsync(lote, _ct);
            await new UnitOfWork(context).CommitAsync(_ct);
            return emails;
        }

        [Fact]
        public async Task ProcessarTresEmails_DeveGravarStatusTentativasECodigoDeErro()
        {
            var emails = await CriarPendentesAsync(3);
            _sender.Resultados[emails[0].Destinatario] = new ResultadoEnvio(true, null);
            _sender.Resultados[emails[1].Destinatario] = new ResultadoEnvio(false, "DESTINATARIO_REJEITADO");
            _sender.Excecoes[emails[2].Destinatario] = new FalhaTemporariaEnvioException("smtp fora");

            await using var provider = CriarProvider();
            var resumo = await provider.GetRequiredService<ProcessarEmailsPendentesHandler>()
                .HandleAsync(new ProcessarEmailsPendentesCommand(), _ct);

            Assert.Equal(new ResumoProcessamento(3, 1, 1, 1, 0), resumo);

            await using var context = CriarContexto();
            var salvos = await new EmailRepository(context).ObterPorStatusAsync(EmailStatusEnum.Enviado, 10, _ct);
            Assert.Equal(emails[0].Id, Assert.Single(salvos).Id);

            var tentativas = await context.Database
                .SqlQuery<LinhaTentativa>($"SELECT EmailId, Numero, Sucesso, CodigoErro FROM TentativasEnvio")
                .ToListAsync(_ct);
            Assert.Equal(3, tentativas.Count);
            Assert.All(tentativas, t => Assert.Equal(1, t.Numero));
            Assert.True(tentativas.Single(t => t.EmailId == emails[0].Id).Sucesso);
            Assert.Null(tentativas.Single(t => t.EmailId == emails[0].Id).CodigoErro);
            Assert.Equal("DESTINATARIO_REJEITADO", tentativas.Single(t => t.EmailId == emails[1].Id).CodigoErro);
            Assert.Equal("SMTP_INDISPONIVEL", tentativas.Single(t => t.EmailId == emails[2].Id).CodigoErro);

            var status = await context.Database
                .SqlQuery<string>($"SELECT Status AS Value FROM Emails WHERE Id = {emails[1].Id}")
                .SingleAsync(_ct);
            Assert.Equal("Falhou", status);
        }

        [Fact]
        public async Task ProcessamentosSimultaneosDoMesmoEmail_DevemEnviarApenasUmaVez()
        {
            var email = (await CriarPendentesAsync(1)).Single();
            _sender.Atraso = TimeSpan.FromMilliseconds(300);

            await using var provider = CriarProvider();
            var resultados = await Task.WhenAll(Enumerable.Range(0, 2).Select(async _ =>
            {
                await using var scope = provider.CreateAsyncScope();
                return await scope.ServiceProvider.GetRequiredService<ProcessarEmailHandler>()
                    .HandleAsync(email.Id, _ct);
            }));

            Assert.Equal(1, resultados.Count(r => r == ResultadoProcessamento.Enviado));
            Assert.Equal(1, resultados.Count(r => r == ResultadoProcessamento.Ignorado));
            Assert.Equal(1, _sender.Envios);

            await using var context = CriarContexto();
            var salvo = await new EmailRepository(context).ObterPorIdAsync(email.Id, _ct);
            Assert.Equal(EmailStatusEnum.Enviado, salvo!.Status);
            Assert.Single(salvo.Tentativas);
        }

        [Fact]
        public async Task SenderFalhaDuasVezesEDepoisEnvia_DeveGravarTresTentativasEStatusEnviado()
        {
            var email = (await CriarPendentesAsync(1)).Single();
            _sender.Sequencia[email.Destinatario] = new Queue<Func<ResultadoEnvio>>([
                () => throw new FalhaTemporariaEnvioException("fora"),
                () => throw new FalhaTemporariaEnvioException("fora"),
                () => new ResultadoEnvio(true, null)]);
            var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
            await using var provider = CriarProvider(clock);
            var processador = provider.GetRequiredService<ProcessarEmailsPendentesHandler>();

            var c1 = await processador.HandleAsync(new ProcessarEmailsPendentesCommand(), _ct);
            var c1b = await processador.HandleAsync(new ProcessarEmailsPendentesCommand(), _ct); // ainda não venceu
            clock.Advance(TimeSpan.FromSeconds(15));
            var c2 = await processador.HandleAsync(new ProcessarEmailsPendentesCommand(), _ct);
            clock.Advance(TimeSpan.FromSeconds(60));
            var c3 = await processador.HandleAsync(new ProcessarEmailsPendentesCommand(), _ct);

            Assert.Equal(new ResumoProcessamento(1, 0, 0, 1, 0), c1);
            Assert.Equal(new ResumoProcessamento(0, 0, 0, 0, 0), c1b);
            Assert.Equal(new ResumoProcessamento(1, 0, 0, 1, 0), c2);
            Assert.Equal(new ResumoProcessamento(1, 1, 0, 0, 0), c3);

            await using var context = CriarContexto();
            var salvo = await new EmailRepository(context).ObterPorIdAsync(email.Id, _ct);
            Assert.Equal(EmailStatusEnum.Enviado, salvo!.Status);
            Assert.Equal(3, salvo.Tentativas.Count);
            Assert.Null(salvo.ProximaTentativaEm);
            var linhas = await context.Database.SqlQuery<int>($"SELECT COUNT(*) AS Value FROM TentativasEnvio WHERE EmailId = {email.Id}").SingleAsync(_ct);
            Assert.Equal(3, linhas);
            Assert.Equal(3, _sender.Envios);
        }

        [Fact]
        public async Task SenderSempreTemporario_DeveTerminarFalhouAposMaxTentativas()
        {
            var email = (await CriarPendentesAsync(1)).Single();
            _sender.Excecoes[email.Destinatario] = new FalhaTemporariaEnvioException("fora");
            var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
            await using var provider = CriarProvider(clock);
            var processador = provider.GetRequiredService<ProcessarEmailsPendentesHandler>();

            for (var i = 0; i < 3; i++)
            {
                await processador.HandleAsync(new ProcessarEmailsPendentesCommand(), _ct);
                clock.Advance(TimeSpan.FromSeconds(60));
            }
            var extra = await processador.HandleAsync(new ProcessarEmailsPendentesCommand(), _ct);

            Assert.Equal(new ResumoProcessamento(0, 0, 0, 0, 0), extra);
            Assert.Equal(3, _sender.Envios);

            await using var context = CriarContexto();
            var salvo = await new EmailRepository(context).ObterPorIdAsync(email.Id, _ct);
            Assert.Equal(EmailStatusEnum.Falhou, salvo!.Status);
            Assert.Equal(3, salvo.Tentativas.Count);
            Assert.Null(salvo.ProximaTentativaEm);
        }

        private sealed record LinhaTentativa(Guid EmailId, int Numero, bool Sucesso, string? CodigoErro);

        private sealed class FakeEmailSender : IEmailSender
        {
            private int _envios;

            public ConcurrentDictionary<string, ResultadoEnvio> Resultados { get; } = new();
            public ConcurrentDictionary<string, Exception> Excecoes { get; } = new();
            public ConcurrentDictionary<string, Queue<Func<ResultadoEnvio>>> Sequencia { get; } = new();
            public TimeSpan Atraso { get; set; }
            public int Envios => _envios;

            public async Task<ResultadoEnvio> EnviarAsync(Email email, LoteEnvio lote, CancellationToken ct)
            {
                Interlocked.Increment(ref _envios);
                if (Atraso > TimeSpan.Zero)
                    await Task.Delay(Atraso, ct);

                if (Sequencia.TryGetValue(email.Destinatario, out var fila) && fila.Count > 0)
                    return fila.Dequeue()();

                if (Excecoes.TryGetValue(email.Destinatario, out var excecao))
                    throw excecao;

                return Resultados.TryGetValue(email.Destinatario, out var resultado)
                    ? resultado
                    : new ResultadoEnvio(true, null);
            }
        }
    }
}
