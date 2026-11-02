using EmailSender.Domain.Entities;
using EmailSender.Domain.Enum;
using EmailSender.Infrastructure.Persistence;
using EmailSender.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace EmailSender.IntegrationTests
{
    public class EmailRepositoryTests : IAsyncLifetime
    {
        private readonly string _database = $"EmailSenderTests_{Guid.NewGuid():N}";
        private readonly CancellationToken _ct = CancellationToken.None;

        // Sobrescrevível via variável de ambiente (ex.: CI com SQL Server em container).
        private static readonly string _servidor = Environment.GetEnvironmentVariable("EMAILSENDER_TESTS_SQLSERVER")
            ?? "Server=(localdb)\\MSSQLLocalDB;Trusted_Connection=True;TrustServerCertificate=True";

        private AppDbContext CriarContexto()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlServer($"{_servidor.TrimEnd(';')};Database={_database}")
                .Options;
            return new AppDbContext(options);
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

        private async Task<LoteEnvio> SalvarLoteAsync()
        {
            var lote = new LoteEnvio("Assunto", "Remetente", corpoTexto: "Corpo");
            await using var context = CriarContexto();
            await new LoteEnvioRepository(context).AdicionarAsync(lote, _ct);
            await new UnitOfWork(context).CommitAsync(_ct);
            return lote;
        }

        [Fact]
        public async Task AdicionarEObterPorId_DevePersistirEmailComTentativa()
        {
            var lote = await SalvarLoteAsync();
            var email = new Email(lote.Id, "a@teste.com", "Nome", "{\"k\":\"v\"}");
            email.Enfileirar();
            email.AdicionarTentativa(DateTimeOffset.UtcNow, sucesso: true);

            await using (var context = CriarContexto())
            {
                await new EmailRepository(context).AdicionarAsync(email, _ct);
                await new UnitOfWork(context).CommitAsync(_ct);
            }

            await using (var context = CriarContexto())
            {
                var salvo = await new EmailRepository(context).ObterPorIdAsync(email.Id, _ct);

                Assert.NotNull(salvo);
                Assert.Equal("a@teste.com", salvo.Destinatario);
                Assert.Equal("{\"k\":\"v\"}", salvo.VariavelJson);
                Assert.Equal(EmailStatusEnum.Enviado, salvo.Status);
                Assert.Single(salvo.Tentativas);

                var statusNoBanco = await context.Database
                    .SqlQuery<string>($"SELECT Status AS Value FROM Emails WHERE Id = {email.Id}")
                    .SingleAsync();
                Assert.Equal("Enviado", statusNoBanco);
            }
        }

        [Fact]
        public async Task ObterPorId_Inexistente_DeveRetornarNulo()
        {
            await using var context = CriarContexto();

            Assert.Null(await new EmailRepository(context).ObterPorIdAsync(Guid.NewGuid(), _ct));
        }

        [Fact]
        public async Task ObterPorStatus_DeveFiltrarELimitar()
        {
            var lote = await SalvarLoteAsync();
            var pendentes = Enumerable.Range(0, 3)
                .Select(i => new Email(lote.Id, $"p{i}@teste.com", "Nome", "{}")).ToList();
            var enfileirado = new Email(lote.Id, "q@teste.com", "Nome", "{}");
            enfileirado.Enfileirar();

            await using (var context = CriarContexto())
            {
                var repo = new EmailRepository(context);
                foreach (var e in pendentes.Append(enfileirado))
                    await repo.AdicionarAsync(e, _ct);
                await new UnitOfWork(context).CommitAsync(_ct);
            }

            await using (var context = CriarContexto())
            {
                var repo = new EmailRepository(context);

                var todos = await repo.ObterPorStatusAsync(EmailStatusEnum.Pendente, 10, _ct);
                var limitados = await repo.ObterPorStatusAsync(EmailStatusEnum.Pendente, 2, _ct);

                Assert.Equal(3, todos.Count);
                Assert.All(todos, e => Assert.Equal(EmailStatusEnum.Pendente, e.Status));
                Assert.Equal(2, limitados.Count);
            }
        }

        [Fact]
        public async Task DoisWorkers_EnfileirandoOMesmoEmail_SoUmConsegue()
        {
            var lote = await SalvarLoteAsync();
            var email = new Email(lote.Id, "c@teste.com", "Nome", "{}");
            await using (var context = CriarContexto())
            {
                await new EmailRepository(context).AdicionarAsync(email, _ct);
                await new UnitOfWork(context).CommitAsync(_ct);
            }

            await using var contextoA = CriarContexto();
            await using var contextoB = CriarContexto();
            var emailA = (await new EmailRepository(contextoA).ObterPorStatusAsync(EmailStatusEnum.Pendente, 10, _ct)).Single();
            var emailB = (await new EmailRepository(contextoB).ObterPorStatusAsync(EmailStatusEnum.Pendente, 10, _ct)).Single();

            emailA.Enfileirar();
            await new UnitOfWork(contextoA).CommitAsync(_ct);

            emailB.Enfileirar();
            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => new UnitOfWork(contextoB).CommitAsync(_ct));
        }

        [Fact]
        public async Task ObterPorStatus_DeveOrdenarPorOrdemDeCriacao()
        {
            var lote = await SalvarLoteAsync();
            var ids = new List<Guid>();
            for (var i = 0; i < 3; i++)
            {
                var email = new Email(lote.Id, $"o{i}@teste.com", "Nome", "{}");
                ids.Add(email.Id);
                await using var context = CriarContexto();
                await new EmailRepository(context).AdicionarAsync(email, _ct);
                await new UnitOfWork(context).CommitAsync(_ct);
                await Task.Delay(20, _ct);
            }

            await using var leitura = CriarContexto();
            var resultado = await new EmailRepository(leitura).ObterPorStatusAsync(EmailStatusEnum.Pendente, 10, _ct);

            Assert.Equal(ids, resultado.Select(e => e.Id));
        }

        [Fact]
        public async Task LoteRepository_DeveBuscarPorIdempotencyKey()
        {
            var lote = new LoteEnvio("Assunto", "Remetente", idempotencyKey: "chave-1", corpoTexto: "Corpo");
            await using (var context = CriarContexto())
            {
                await new LoteEnvioRepository(context).AdicionarAsync(lote, _ct);
                await new UnitOfWork(context).CommitAsync(_ct);
            }

            await using (var context = CriarContexto())
            {
                var repo = new LoteEnvioRepository(context);

                Assert.Equal(lote.Id, (await repo.ObterPorIdempotencyKeyAsync("chave-1", _ct))!.Id);
                Assert.Null(await repo.ObterPorIdempotencyKeyAsync("outra", _ct));
            }
        }
    }
}
