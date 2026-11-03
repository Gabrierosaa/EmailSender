using System.Net.Sockets;
using EmailSender.Application.Abstractions;
using EmailSender.Domain.Entities;
using EmailSender.Infrastructure.Settings;
using MailKit;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace EmailSender.Infrastructure.Services
{
    public sealed class SmtpEmailSender : IEmailSender
    {
        private readonly SmtpSettings _settings;

        public SmtpEmailSender(IOptions<SmtpSettings> options)
        {
            _settings = options.Value;
        }

        public async Task<ResultadoEnvio> EnviarAsync(Email email, LoteEnvio lote, CancellationToken ct)
        {
            var mensagem = MontarMensagem(email, lote);

            using var client = new SmtpClient { Timeout = _settings.TimeoutSegundos * 1000 };

            try
            {
                await client.ConnectAsync(_settings.Host, _settings.Porta, _settings.Seguranca, ct);

                if (!string.IsNullOrWhiteSpace(_settings.Usuario))
                    await client.AuthenticateAsync(_settings.Usuario, _settings.Senha ?? "", ct);

                await client.SendAsync(mensagem, ct);
                await client.DisconnectAsync(true, ct);

                return new ResultadoEnvio(true, null);
            }
            catch (SmtpCommandException ex) when ((int)ex.StatusCode >= 500)
            {
                return new ResultadoEnvio(false, $"SMTP_{(int)ex.StatusCode}");
            }
            catch (SmtpCommandException ex)
            {
                throw new FalhaTemporariaEnvioException($"Falha temporária do servidor SMTP (SMTP_{(int)ex.StatusCode}).", ex);
            }
            catch (Exception ex) when (ex is SocketException
                or IOException
                or TimeoutException
                or SslHandshakeException
                or ServiceNotConnectedException)
            {
                throw new FalhaTemporariaEnvioException($"Falha temporária de comunicação com o servidor SMTP ({ex.GetType().Name}).", ex);
            }
        }

        internal MimeMessage MontarMensagem(Email email, LoteEnvio lote)
        {
            var mensagem = new MimeMessage
            {
                Subject = lote.Assunto,
                MessageId = $"{email.Id}@emailsender"
            };

            mensagem.From.Add(new MailboxAddress(lote.RemetenteNome, _settings.RemetenteEndereco));
            mensagem.To.Add(new MailboxAddress(email.NomeDestinatario, email.Destinatario));

            if (!string.IsNullOrWhiteSpace(lote.ReplyTo))
                mensagem.ReplyTo.Add(MailboxAddress.Parse(lote.ReplyTo));

            mensagem.Body = new BodyBuilder
            {
                HtmlBody = lote.CorpoHtml,
                TextBody = lote.CorpoTexto
            }.ToMessageBody();

            return mensagem;
        }
    }
}
