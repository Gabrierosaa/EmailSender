using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using EmailSender.Application.Abstractions;
using EmailSender.Domain.Entities;
using EmailSender.Domain.Enum;
using EmailSender.Infrastructure.Persistence;
using EmailSender.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EmailSender.IntegrationTests
{
    /// <summary>
    /// POST /api/v1/emails de ponta a ponta: Api real (Kestrel) + SQL Server em container.
    /// SMTP e RabbitMQ não estão no ar em nenhum teste desta classe.
    /// </summary>
    [Collection(SqlServerCollection.Nome)]
    public class EmailsEndpointTests : IAsyncLifetime
    {
        private const string Rota = "/api/v1/emails";

        private readonly ApiFactory _factory;
        private HttpClient _client = null!;

        public EmailsEndpointTests(SqlServerFixture sql)
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

        private static object RequisicaoValida(string destinatario = "fulano@teste.com") => new
        {
            destinatario,
            nomeDestinatario = "Fulano",
            assunto = "Olá",
            remetenteNome = "Equipe",
            corpoTexto = "Bem-vindo!",
            variaveis = new Dictionary<string, string> { ["nome"] = "Fulano" }
        };

        private Task<HttpResponseMessage> PostarAsync(object corpo) => _client.PostAsJsonAsync(Rota, corpo);

        private Task<HttpResponseMessage> PostarJsonBrutoAsync(string json) =>
            _client.PostAsync(Rota, new StringContent(json, Encoding.UTF8, "application/json"));

        private static async Task<Dictionary<string, string[]>> ErrosPorCampoAsync(HttpResponseMessage resposta)
        {
            using var doc = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
            return doc.RootElement.GetProperty("errors").EnumerateObject()
                .ToDictionary(p => p.Name, p => p.Value.EnumerateArray().Select(m => m.GetString()!).ToArray());
        }

        private async Task AssertNadaGravadoAsync()
        {
            var (lotes, emails) = await _factory.ContarRegistrosAsync();
            Assert.Equal(0, lotes);
            Assert.Equal(0, emails);
        }

        // ---------- 201 ----------

        [Fact]
        public async Task Post_Valido_Retorna201ComIdELocation()
        {
            var resposta = await PostarAsync(RequisicaoValida());

            Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
            var corpo = await resposta.Content.ReadFromJsonAsync<JsonElement>();
            var id = corpo.GetProperty("id").GetGuid();
            Assert.NotEqual(Guid.Empty, id);
            Assert.Equal($"/api/v1/emails/{id}", resposta.Headers.Location!.OriginalString);
        }

        [Fact]
        public async Task Post_Valido_GravaLoteUnitarioComEmailPendente()
        {
            var resposta = await PostarAsync(RequisicaoValida());
            var id = (await resposta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

            await using var context = new AppDbContext(
                new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(_factory.ConnectionString).Options);

            var (lotes, emails) = await _factory.ContarRegistrosAsync();
            Assert.Equal(1, lotes);
            Assert.Equal(1, emails);

            var email = await context.Emails.SingleAsync(e => e.Id == id);
            Assert.Equal("fulano@teste.com", email.Destinatario);
            Assert.Equal("Fulano", email.NomeDestinatario);
            Assert.Equal("{\"nome\":\"Fulano\"}", email.VariavelJson);
            Assert.Equal(EmailStatusEnum.Pendente, email.Status);

            var statusNoBanco = await context.Database
                .SqlQuery<string>($"SELECT Status AS Value FROM Emails WHERE Id = {id}")
                .SingleAsync();
            Assert.Equal("Pendente", statusNoBanco);

            var tentativas = await context.Database
                .SqlQuery<int>($"SELECT COUNT(*) AS Value FROM TentativasEnvio WHERE EmailId = {id}")
                .SingleAsync();
            Assert.Equal(0, tentativas);

            var lote = await context.Lotes.SingleAsync(l => l.Id == email.LoteId);
            Assert.Equal("Olá", lote.Assunto);
            Assert.Equal("Equipe", lote.RemetenteNome);
        }

        [Fact]
        public async Task Post_ComSmtpERabbitMqForaDoAr_Retorna201()
        {
            // A fábrica aponta SMTP e RabbitMQ para 127.0.0.1:1 (ninguém escutando) e este fluxo só registra o e-mail.
            using var scope = _factory.Services.CreateScope();
            Assert.Null(scope.ServiceProvider.GetService<IEmailSender>());

            var resposta = await PostarAsync(RequisicaoValida());

            Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
            var (_, emails) = await _factory.ContarRegistrosAsync();
            Assert.Equal(1, emails);
        }

        // ---------- GET /{id}/status ----------

        [Fact]
        public async Task GetStatus_AposPost_Retorna200ComStatusPendenteESemDestinatario()
        {
            var criado = await PostarAsync(RequisicaoValida());
            var id = (await criado.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

            var resposta = await _client.GetAsync($"{Rota}/{id}/status");

            Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
            var json = await resposta.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            Assert.Equal(id, doc.RootElement.GetProperty("id").GetGuid());
            Assert.Equal("Pendente", doc.RootElement.GetProperty("status").GetString());
            Assert.Equal(JsonValueKind.Null, doc.RootElement.GetProperty("ultimaTentativaEm").ValueKind);
            Assert.Equal(JsonValueKind.Null, doc.RootElement.GetProperty("ultimoErro").ValueKind);
            Assert.DoesNotContain("destinatario", json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("fulano@teste.com", json);
        }

        [Fact]
        public async Task GetStatus_ComTentativas_RetornaUltimaTentativaEErro()
        {
            var inicio = new DateTimeOffset(2026, 11, 3, 10, 0, 0, TimeSpan.Zero);
            var lote = LoteEnvio.CriarUnitario("Assunto", "Equipe", "fulano@teste.com", corpoTexto: "Corpo");
            var email = lote.Emails.Single();
            email.Enfileirar();
            email.AdicionarTentativa(inicio, sucesso: false, "SMTP_TIMEOUT");
            email.Enfileirar();
            email.AdicionarTentativa(inicio.AddMinutes(5), sucesso: false, "SMTP_REJEITADO");

            await using (var context = new AppDbContext(
                new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(_factory.ConnectionString).Options))
            {
                await new LoteEnvioRepository(context).AdicionarAsync(lote, CancellationToken.None);
                await new UnitOfWork(context).CommitAsync(CancellationToken.None);
            }

            var resposta = await _client.GetAsync($"{Rota}/{email.Id}/status");

            Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
            var corpo = await resposta.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("Falhou", corpo.GetProperty("status").GetString());
            Assert.Equal(inicio.AddMinutes(5), corpo.GetProperty("ultimaTentativaEm").GetDateTimeOffset());
            Assert.Equal("SMTP_REJEITADO", corpo.GetProperty("ultimoErro").GetString());
        }

        [Fact]
        public async Task GetStatus_IdInexistente_Retorna404()
        {
            var resposta = await _client.GetAsync($"{Rota}/{Guid.NewGuid()}/status");

            Assert.Equal(HttpStatusCode.NotFound, resposta.StatusCode);
        }

        [Fact]
        public async Task GetStatus_IdNaoGuid_Retorna404()
        {
            var resposta = await _client.GetAsync($"{Rota}/abc/status");

            Assert.Equal(HttpStatusCode.NotFound, resposta.StatusCode);
        }

        // ---------- 400 ----------

        [Fact]
        public async Task Post_SemCamposObrigatorios_Retorna400ComErrosPorCampoENadaGravado()
        {
            var resposta = await PostarJsonBrutoAsync("{}");

            Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
            Assert.Equal("application/problem+json", resposta.Content.Headers.ContentType!.MediaType);
            var erros = await ErrosPorCampoAsync(resposta);
            Assert.Contains("O destinatário é obrigatório.", erros["destinatario"]);
            Assert.Contains("O assunto é obrigatório.", erros["assunto"]);
            Assert.Contains("O nome do remetente é obrigatório.", erros["remetenteNome"]);
            Assert.Contains("Informe o corpo do e-mail em HTML ou em texto.", erros["corpo"]);
            await AssertNadaGravadoAsync();
        }

        [Theory]
        [InlineData("sem-arroba")]
        [InlineData("@teste.com")]
        [InlineData("a@")]
        public async Task Post_EmailInvalido_Retorna400ENadaGravado(string destinatario)
        {
            var resposta = await PostarAsync(RequisicaoValida(destinatario));

            Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
            var erros = await ErrosPorCampoAsync(resposta);
            Assert.Contains("O destinatário informado não é um e-mail válido.", erros["destinatario"]);
            await AssertNadaGravadoAsync();
        }

        [Fact]
        public async Task Post_QuebraDeLinhaNoDestinatario_Retorna400ENadaGravado()
        {
            var resposta = await PostarAsync(RequisicaoValida("a@teste.com\r\nBcc: outro@teste.com"));

            Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
            var erros = await ErrosPorCampoAsync(resposta);
            Assert.Contains("O destinatário não pode conter quebra de linha.", erros["destinatario"]);
            await AssertNadaGravadoAsync();
        }

        [Fact]
        public async Task Post_QuebraDeLinhaNoAssunto_Retorna400ENadaGravado()
        {
            var corpo = new
            {
                destinatario = "a@teste.com",
                assunto = "Oi\r\nBcc: outro@teste.com",
                remetenteNome = "Equipe",
                corpoTexto = "Corpo"
            };

            var resposta = await PostarAsync(corpo);

            Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
            var erros = await ErrosPorCampoAsync(resposta);
            Assert.Contains("O assunto não pode conter quebra de linha.", erros["assunto"]);
            await AssertNadaGravadoAsync();
        }

        [Fact]
        public async Task Post_JsonMalformado_Retorna400ENadaGravado()
        {
            var resposta = await PostarJsonBrutoAsync("{ quebrado");

            Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
            await AssertNadaGravadoAsync();
        }

        // ---------- 413 ----------

        [Fact]
        public async Task Post_CorpoAcimaDe1MB_Retorna413ENadaGravado()
        {
            var json = "{\"destinatario\":\"a@teste.com\",\"assunto\":\"x\",\"remetenteNome\":\"y\",\"corpoTexto\":\""
                + new string('a', 1024 * 1024 + 1) + "\"}";

            var resposta = await PostarJsonBrutoAsync(json);

            Assert.Equal(HttpStatusCode.RequestEntityTooLarge, resposta.StatusCode);
            await AssertNadaGravadoAsync();
        }

        [Fact]
        public async Task Post_CorpoAbaixoDoLimite_Retorna201()
        {
            var corpo = new
            {
                destinatario = "a@teste.com",
                assunto = "x",
                remetenteNome = "y",
                corpoTexto = new string('a', 900 * 1024)
            };

            var resposta = await PostarAsync(corpo);

            Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
        }
    }
}
