using EmailSender.Domain.Entities;
using EmailSender.Domain.Enum;
using EmailSender.Infrastructure.Persistence;
using EmailSender.Infrastructure.Persistence.Repositories;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace EmailSender.IntegrationTests
{
    [Collection(SqlServerCollection.Nome)]
    public class LoteEnvioRepositoryTests : IAsyncLifetime
    {
        private readonly string _database = $"EmailSenderTests_{Guid.NewGuid():N}";
        private readonly CancellationToken _ct = CancellationToken.None;
        private readonly string _servidor;

        public LoteEnvioRepositoryTests(SqlServerFixture sql)
        {
            _servidor = sql.ConnectionString;
        }

        private AppDbContext CriarContexto()
        {
            var connectionString = new SqlConnectionStringBuilder(_servidor) { InitialCatalog = _database }.ConnectionString;
            return new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(connectionString).Options);
        }

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

        [Fact]
        public async Task ObterResumo_ComEmailsEmEstadosDiferentes_DeveContarPorStatus()
        {
            var lote = new LoteEnvio("Assunto", "Remetente", corpoTexto: "Corpo");
            lote.AdicionarEmail(new Email(lote.Id, "p1@teste.com", "P", "{}"));
            lote.AdicionarEmail(new Email(lote.Id, "p2@teste.com", "P", "{}"));
            var enfileirado = new Email(lote.Id, "q@teste.com", "Q", "{}");
            enfileirado.Enfileirar();
            lote.AdicionarEmail(enfileirado);
            var enviado = new Email(lote.Id, "e@teste.com", "E", "{}");
            enviado.Enfileirar();
            enviado.RegistrarSucesso(DateTimeOffset.UtcNow);
            lote.AdicionarEmail(enviado);
            var falho = new Email(lote.Id, "f@teste.com", "F", "{}");
            falho.Enfileirar();
            falho.RegistrarFalha(DateTimeOffset.UtcNow, "ERRO");
            lote.AdicionarEmail(falho);

            await using (var context = CriarContexto())
            {
                await new LoteEnvioRepository(context).AdicionarAsync(lote, _ct);
                await new UnitOfWork(context).CommitAsync(_ct);
            }

            await using var leitura = CriarContexto();
            var resumo = await new LoteEnvioRepository(leitura).ObterResumoAsync(lote.Id, _ct);

            Assert.NotNull(resumo);
            Assert.Equal(lote.Id, resumo!.Id);
            Assert.Equal(lote.CriadoEm.ToUnixTimeSeconds(), resumo.CriadoEm.ToUnixTimeSeconds());
            Assert.Equal(2, resumo.ContagemPorStatus[EmailStatusEnum.Pendente]);
            Assert.Equal(1, resumo.ContagemPorStatus[EmailStatusEnum.Enfileirado]);
            Assert.Equal(1, resumo.ContagemPorStatus[EmailStatusEnum.Enviado]);
            Assert.Equal(1, resumo.ContagemPorStatus[EmailStatusEnum.Falhou]);
            Assert.Empty(leitura.ChangeTracker.Entries());
        }

        [Fact]
        public async Task ObterResumo_LoteInexistente_DeveRetornarNull()
        {
            await using var context = CriarContexto();

            Assert.Null(await new LoteEnvioRepository(context).ObterResumoAsync(Guid.NewGuid(), _ct));
        }
    }
}
