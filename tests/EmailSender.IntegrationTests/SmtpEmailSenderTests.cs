using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using EmailSender.Application.Abstractions;
using EmailSender.Domain.Entities;
using EmailSender.Infrastructure.Services;
using EmailSender.Infrastructure.Settings;
using MailKit.Security;
using Microsoft.Extensions.Options;

namespace EmailSender.IntegrationTests
{
    public class SmtpEmailSenderTests
    {
        private static SmtpEmailSender CriarSender(int porta) => new(Options.Create(new SmtpSettings
        {
            Host = "localhost",
            Porta = porta,
            RemetenteEndereco = "teste@localhost",
            Seguranca = SecureSocketOptions.None,
            TimeoutSegundos = 5
        }));

        private static (Email, LoteEnvio) CriarLote(string assunto)
        {
            var lote = LoteEnvio.CriarUnitario(assunto, "Remetente", "dest@exemplo.com", "Destino", corpoTexto: "Olá");
            return (lote.Emails.Single(), lote);
        }

        [Fact]
        public async Task Mailpit_EnviaEmailEAparecenoPainel()
        {
            var assunto = $"Teste {Guid.NewGuid():N}";
            var (email, lote) = CriarLote(assunto);

            var resultado = await CriarSender(1025).EnviarAsync(email, lote, CancellationToken.None);

            Assert.True(resultado.Sucesso);
            Assert.Null(resultado.CodigoErro);

            using var http = new HttpClient { BaseAddress = new Uri("http://localhost:8025") };
            var json = await http.GetFromJsonAsync<JsonElement>($"/api/v1/search?query={Uri.EscapeDataString($"subject:\"{assunto}\"")}");
            Assert.Equal(1, json.GetProperty("messages_count").GetInt32());
        }

        [Fact]
        public async Task PortaFechada_LancaFalhaTemporariaEnvioException()
        {
            var (email, lote) = CriarLote("Assunto");

            await Assert.ThrowsAsync<FalhaTemporariaEnvioException>(
                () => CriarSender(1).EnviarAsync(email, lote, CancellationToken.None));
        }

        [Fact]
        public async Task Smtp550_RetornaFalhaPermanente()
        {
            using var servidor = new TcpListener(IPAddress.Loopback, 0);
            servidor.Start();
            var porta = ((IPEndPoint)servidor.LocalEndpoint).Port;
            var atendimento = AtenderComRejeicao550(servidor);

            var (email, lote) = CriarLote("Assunto");
            var resultado = await CriarSender(porta).EnviarAsync(email, lote, CancellationToken.None);

            Assert.False(resultado.Sucesso);
            Assert.Equal("SMTP_550", resultado.CodigoErro);
            await atendimento;
        }

        private static async Task AtenderComRejeicao550(TcpListener servidor)
        {
            using var cliente = await servidor.AcceptTcpClientAsync();
            using var stream = cliente.GetStream();
            using var reader = new StreamReader(stream, Encoding.ASCII);
            using var writer = new StreamWriter(stream, Encoding.ASCII) { NewLine = "\r\n", AutoFlush = true };

            await writer.WriteLineAsync("220 fake ESMTP");
            string? linha;
            while ((linha = await reader.ReadLineAsync()) is not null)
            {
                if (linha.StartsWith("EHLO", StringComparison.OrdinalIgnoreCase) || linha.StartsWith("HELO", StringComparison.OrdinalIgnoreCase))
                    await writer.WriteLineAsync("250 fake");
                else if (linha.StartsWith("QUIT", StringComparison.OrdinalIgnoreCase))
                {
                    await writer.WriteLineAsync("221 bye");
                    break;
                }
                else
                    await writer.WriteLineAsync("550 rejeitado");
            }
        }
    }
}
