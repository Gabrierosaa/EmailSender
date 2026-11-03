using EmailSender.Domain.Entities;
using EmailSender.Domain.Enum;
using EmailSender.Domain.Exceptions;
using EmailSender.Infrastructure.Persistence;
using EmailSender.Infrastructure.Persistence.Repositories;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace EmailSender.IntegrationTests
{
    [Collection(SqlServerCollection.Nome)]
    public class EmailRepositoryTests : IAsyncLifetime
    {
        private readonly string _database = $"EmailSenderTests_{Guid.NewGuid():N}";
        private readonly CancellationToken _ct = CancellationToken.None;
        private readonly string _servidor;

        public EmailRepositoryTests(SqlServerFixture sql)
        {
            _servidor = sql.ConnectionString;
        }

        private AppDbContext CriarContexto()
        {
            var connectionString = new SqlConnectionStringBuilder(_servidor) { InitialCatalog = _database }.ConnectionString;
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlServer(connectionString)
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
        public async Task ObterPorId_DeveCarregarTodasAsTentativas()
        {
            var lote = await SalvarLoteAsync();
            var email = new Email(lote.Id, "t@teste.com", "Nome", "{}");
            email.Enfileirar();
            email.AdicionarTentativa(DateTimeOffset.UtcNow.AddMinutes(-5), sucesso: false, "SMTP_TIMEOUT");
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
                Assert.Equal(new[] { 1, 2 }, salvo.Tentativas.Select(t => t.Numero).OrderBy(n => n));
                Assert.Equal("SMTP_TIMEOUT", salvo.Tentativas.Single(t => t.Numero == 1).CodigoErro);
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
            await Assert.ThrowsAsync<ConflitoConcorrenciaException>(() => new UnitOfWork(contextoB).CommitAsync(_ct));
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
        public async Task LoteUnitario_DevePersistirLoteComUmEmailPendente()
        {
            var lote = LoteEnvio.CriarUnitario("Assunto", "Remetente", "a@teste.com",
                nomeDestinatario: "Fulano", corpoTexto: "Corpo");

            await using (var context = CriarContexto())
            {
                await new LoteEnvioRepository(context).AdicionarAsync(lote, _ct);
                await new UnitOfWork(context).CommitAsync(_ct);
            }

            await using (var context = CriarContexto())
            {
                var salvo = await new LoteEnvioRepository(context).ObterPorIdAsync(lote.Id, _ct);

                Assert.NotNull(salvo);
                var email = Assert.Single(salvo.Emails);
                Assert.Equal("a@teste.com", email.Destinatario);
                Assert.Equal(EmailStatusEnum.Pendente, email.Status);
                Assert.Empty(email.Tentativas);

                var linha = await context.Database
                    .SqlQuery<string>($"SELECT Status AS Value FROM Emails WHERE LoteId = {lote.Id}")
                    .SingleAsync();
                Assert.Equal("Pendente", linha);
            }
        }

        [Fact]
        public async Task MesmoDestinatarioNoMesmoLote_DeveSerRejeitadoPeloBanco()
        {
            var lote = await SalvarLoteAsync();
            await using var context = CriarContexto();
            var repo = new EmailRepository(context);
            await repo.AdicionarAsync(new Email(lote.Id, "dup@teste.com", "A", "{}"), _ct);
            await repo.AdicionarAsync(new Email(lote.Id, "dup@teste.com", "B", "{}"), _ct);

            await Assert.ThrowsAsync<DbUpdateException>(() => new UnitOfWork(context).CommitAsync(_ct));
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
