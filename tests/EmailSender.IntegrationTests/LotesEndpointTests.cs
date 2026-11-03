using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using EmailSender.Domain.Entities;
using EmailSender.Domain.Enum;
using EmailSender.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EmailSender.IntegrationTests
{
    [Collection(SqlServerCollection.Nome)]
    public class LotesEndpointTests : IAsyncLifetime
    {
        private const string Rota = "/api/v1/lotes";

        private readonly ApiFactory _factory;
        private HttpClient _client = null!;

        public LotesEndpointTests(SqlServerFixture sql)
        {
            _factory = new ApiFactory(sql.ConnectionString);
        }

        public async Task InitializeAsync()
        {
            await _factory.MigrarAsync();
            _client = _factory.CreateClient();
        }

        public async Task DisposeAsync()
        {
            _client.Dispose();
            await _factory.ApagarBancoAsync();
            await _factory.DisposeAsync();
        }

        private AppDbContext CriarContexto() =>
            new(new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(_factory.ConnectionString).Options);

        private static object Requisicao(IEnumerable<string> emails) => new
        {
            assunto = "Olá",
            remetenteNome = "Equipe",
            corpoTexto = "Bem-vindo!",
            destinatarios = emails.Select(e => new
            {
                email = e,
                nome = "Fulano",
                variaveis = new Dictionary<string, string> { ["codigo"] = "A123" }
            }).ToArray()
        };

        [Fact]
        public async Task Post_TresDestinatarios_Retorna202ComLocationEGravaTresPendentes()
        {
            var resposta = await _client.PostAsJsonAsync(Rota, Requisicao(new[] { "a@teste.com", "b@teste.com", "c@teste.com" }));

            Assert.Equal(HttpStatusCode.Accepted, resposta.StatusCode);
            var corpo = await resposta.Content.ReadFromJsonAsync<JsonElement>();
            var loteId = corpo.GetProperty("loteId").GetGuid();
            Assert.Equal(3, corpo.GetProperty("totalDestinatarios").GetInt32());
            Assert.Equal("Processando", corpo.GetProperty("status").GetString());
            Assert.Equal($"/api/v1/lotes/{loteId}", resposta.Headers.Location!.OriginalString);

            await using var context = CriarContexto();
            var emails = await context.Emails.Where(e => e.LoteId == loteId).ToListAsync();
            Assert.Equal(3, emails.Count);
            Assert.All(emails, e => Assert.Equal(EmailStatusEnum.Pendente, e.Status));
        }

        [Fact]
        public async Task Post_ComUmInvalido_Retorna400ENaoGravaNada()
        {
            var resposta = await _client.PostAsJsonAsync(Rota, Requisicao(new[] { "a@teste.com", "invalido", "c@teste.com" }));

            Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
            using var doc = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
            Assert.True(doc.RootElement.GetProperty("errors").TryGetProperty("destinatarios[1].email", out _));

            var (lotes, emails) = await _factory.ContarRegistrosAsync();
            Assert.Equal(0, lotes);
            Assert.Equal(0, emails);
        }

        [Fact]
        public async Task Post_MilDestinatarios_EAceito()
        {
            var emails = Enumerable.Range(0, 1000).Select(i => $"destinatario.numero.{i:D4}@exemplo-teste.com.br");

            var resposta = await _client.PostAsJsonAsync(Rota, Requisicao(emails));

            Assert.Equal(HttpStatusCode.Accepted, resposta.StatusCode);
            var (lotes, total) = await _factory.ContarRegistrosAsync();
            Assert.Equal(1, lotes);
            Assert.Equal(1000, total);
        }

        [Fact]
        public async Task Get_Existente_Retorna200ComContagens()
        {
            var lote = new LoteEnvio("Assunto", "Remetente", corpoTexto: "Corpo");
            var pendente = new Email(lote.Id, "p@teste.com", "P", "{}");
            var enfileirado = new Email(lote.Id, "q@teste.com", "Q", "{}");
            enfileirado.Enfileirar();
            var enviado = new Email(lote.Id, "e@teste.com", "E", "{}");
            enviado.Enfileirar();
            enviado.RegistrarSucesso(DateTimeOffset.UtcNow);
            var falho = new Email(lote.Id, "f@teste.com", "F", "{}");
            falho.Enfileirar();
            falho.RegistrarFalha(DateTimeOffset.UtcNow, "SMTP_PERMANENTE");
            foreach (var e in new[] { pendente, enfileirado, enviado, falho })
                lote.AdicionarEmail(e);

            await using (var context = CriarContexto())
            {
                context.Lotes.Add(lote);
                await context.SaveChangesAsync();
            }

            var resposta = await _client.GetAsync($"{Rota}/{lote.Id}");

            Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
            var corpo = await resposta.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(lote.Id, corpo.GetProperty("id").GetGuid());
            Assert.Equal(4, corpo.GetProperty("totalDestinatarios").GetInt32());
            Assert.Equal(2, corpo.GetProperty("pendentes").GetInt32());
            Assert.Equal(1, corpo.GetProperty("enviados").GetInt32());
            Assert.Equal(1, corpo.GetProperty("falhos").GetInt32());
            Assert.Equal("Processando", corpo.GetProperty("status").GetString());
        }

        [Fact]
        public async Task Get_Inexistente_Retorna404()
        {
            var resposta = await _client.GetAsync($"{Rota}/{Guid.NewGuid()}");

            Assert.Equal(HttpStatusCode.NotFound, resposta.StatusCode);
        }

        [Fact]
        public async Task Get_IdQueNaoEGuid_Retorna404()
        {
            var resposta = await _client.GetAsync($"{Rota}/abc");

            Assert.Equal(HttpStatusCode.NotFound, resposta.StatusCode);
        }
    }
}
