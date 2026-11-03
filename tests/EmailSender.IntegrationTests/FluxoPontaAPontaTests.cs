using System.Net;
using System.Net.Http.Json;
using EmailSender.Application.Abstractions;
using EmailSender.Application.DTOs;
using EmailSender.Application.Emails.Commands.ProcessarEmailsPendentes;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace EmailSender.IntegrationTests
{
    [Collection(SqlServerCollection.Nome)]
    public class FluxoPontaAPontaTests : IAsyncLifetime
    {
        private readonly FakeEmailSender _sender = new();
        private readonly ApiFactory _factory;
        private HttpClient _client = null!;

        public FluxoPontaAPontaTests(SqlServerFixture sql)
        {
            _factory = new ApiFactory(sql.ConnectionString,
                services =>
                {
                    services.Replace(ServiceDescriptor.Singleton<IEmailSender>(_sender));
                    services.AddSingleton(PoliticaRetry.Padrao);
                });
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

        private static object Requisicao(string destinatario) => new
        {
            destinatario,
            nomeDestinatario = "Fulano",
            assunto = "Olá",
            remetenteNome = "Equipe",
            corpoTexto = "Bem-vindo!"
        };

        private async Task<Guid> PostarEmailAsync(string destinatario)
        {
            var resposta = await _client.PostAsJsonAsync("/api/v1/emails", Requisicao(destinatario));
            Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
            return (await resposta.Content.ReadFromJsonAsync<CriarEmailResponse>())!.Id;
        }

        private async Task<EmailStatusDto> ConsultarStatusAsync(Guid id)
        {
            var resposta = await _client.GetAsync($"/api/v1/emails/{id}/status");
            Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
            return (await resposta.Content.ReadFromJsonAsync<EmailStatusDto>())!;
        }

        private Task<ResumoProcessamento> ProcessarCicloAsync() =>
            _factory.Services.GetRequiredService<IProcessadorEmailsPendentes>()
                .HandleAsync(new ProcessarEmailsPendentesCommand(), CancellationToken.None);

        private async Task<List<(bool Sucesso, string? CodigoErro)>> TentativasAsync(Guid emailId)
        {
            await using var context = new Infrastructure.Persistence.AppDbContext(
                new DbContextOptionsBuilder<Infrastructure.Persistence.AppDbContext>().UseSqlServer(_factory.ConnectionString).Options);
            var linhas = await context.Database
                .SqlQuery<LinhaTentativa>($"SELECT Sucesso, CodigoErro FROM TentativasEnvio WHERE EmailId = {emailId}")
                .ToListAsync();
            return linhas.Select(l => (l.Sucesso, l.CodigoErro)).ToList();
        }

        private sealed record LinhaTentativa(bool Sucesso, string? CodigoErro);

        [Fact]
        public async Task PostEmail_DepoisDoCiclo_StatusDeveSerEnviado()
        {
            var id = await PostarEmailAsync("fulano@teste.com");

            var antes = await ConsultarStatusAsync(id);
            Assert.Equal("Pendente", antes.Status);
            Assert.Null(antes.UltimaTentativaEm);
            Assert.Null(antes.UltimoErro);

            var resumo = await ProcessarCicloAsync();

            Assert.Equal(1, resumo.Enviados);
            var depois = await ConsultarStatusAsync(id);
            Assert.Equal("Enviado", depois.Status);
            Assert.NotNull(depois.UltimaTentativaEm);
            Assert.Null(depois.UltimoErro);

            var tentativa = Assert.Single(await TentativasAsync(id));
            Assert.True(tentativa.Sucesso);
            var chamada = Assert.Single(_sender.Chamadas);
            Assert.Equal("fulano@teste.com", chamada.Email.Destinatario);
            Assert.Equal("Olá", chamada.Lote.Assunto);
        }

        [Fact]
        public async Task FalhaPermanenteDoSender_StatusDeveSerFalhouComOCodigoDeErro()
        {
            _sender.Resultados["rejeitado@teste.com"] = new ResultadoEnvio(false, "SMTP_550");
            var id = await PostarEmailAsync("rejeitado@teste.com");

            var resumo = await ProcessarCicloAsync();

            Assert.Equal(1, resumo.Falhos);
            var status = await ConsultarStatusAsync(id);
            Assert.Equal("Falhou", status.Status);
            Assert.Equal("SMTP_550", status.UltimoErro);
            Assert.NotNull(status.UltimaTentativaEm);
        }

        [Fact]
        public async Task PostLote_DepoisDoCiclo_LoteDeveSerConcluido()
        {
            var resposta = await _client.PostAsJsonAsync("/api/v1/lotes", new
            {
                assunto = "Olá",
                remetenteNome = "Equipe",
                corpoTexto = "Bem-vindo!",
                destinatarios = new[] { new { email = "a@teste.com" }, new { email = "b@teste.com" } }
            });
            Assert.Equal(HttpStatusCode.Accepted, resposta.StatusCode);
            var criado = (await resposta.Content.ReadFromJsonAsync<CriarLoteResponse>())!;

            var resumo = await ProcessarCicloAsync();

            Assert.Equal(2, resumo.Enviados);
            var lote = (await _client.GetFromJsonAsync<LoteStatusDto>($"/api/v1/lotes/{criado.LoteId}"))!;
            Assert.Equal("Concluido", lote.Status);
            Assert.Equal(2, lote.Enviados);
            Assert.Equal(0, lote.Pendentes);
        }
    }
}
