using EmailSender.Application.Abstractions;
using EmailSender.Domain.Entities;
using EmailSender.Domain.Enum;
using EmailSender.Domain.Exceptions;
using EmailSender.Domain.Repositories;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace EmailSender.Application.Emails.Commands.ProcessarEmailsPendentes
{
    /// <summary>
    /// Processa um e-mail Pendente: reserva (Enfileirado), envia e registra a tentativa (Enviado ou Falhou).
    /// </summary>
    public sealed class ProcessarEmailHandler
    {
        public const string CodigoSmtpIndisponivel = "SMTP_INDISPONIVEL";
        public const string CodigoErroInesperado = "ERRO_INESPERADO";
        public const int CodigoErroMaxLength = 100;

        private readonly IEmailRepository _emails;
        private readonly ILoteEnvioRepository _lotes;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IEmailSender _sender;
        private readonly TimeProvider _clock;
        private readonly ILogger<ProcessarEmailHandler> _logger;

        public ProcessarEmailHandler(
            IEmailRepository emails,
            ILoteEnvioRepository lotes,
            IUnitOfWork unitOfWork,
            IEmailSender sender,
            TimeProvider clock,
            ILogger<ProcessarEmailHandler>? logger = null)
        {
            _logger = logger ?? NullLogger<ProcessarEmailHandler>.Instance;
            _emails = emails;
            _lotes = lotes;
            _unitOfWork = unitOfWork;
            _sender = sender;
            _clock = clock;
        }

        public async Task<ResultadoProcessamento> HandleAsync(Guid emailId, CancellationToken ct)
        {
            var email = await _emails.ObterPorIdAsync(emailId, ct);
            if (email is null || email.Status != EmailStatusEnum.Pendente)
                return ResultadoProcessamento.Ignorado;

            var lote = await _lotes.ObterPorIdAsync(email.LoteId, ct);
            if (lote is null)
                return ResultadoProcessamento.Ignorado;

            // O commit "reserva" o e-mail: o RowVersion garante que só um processamento avança.
            email.Enfileirar();
            try
            {
                await _unitOfWork.CommitAsync(ct);
            }
            catch (ConflitoConcorrenciaException)
            {
                return ResultadoProcessamento.Ignorado;
            }

            var inicio = _clock.GetUtcNow();
            var sucesso = await EnviarERegistrarAsync(email, lote, inicio, ct);

            // O envio já ocorreu: o registro do resultado não pode ser abortado por cancelamento.
            await _unitOfWork.CommitAsync(CancellationToken.None);

            return sucesso ? ResultadoProcessamento.Enviado : ResultadoProcessamento.Falhou;
        }

        private async Task<bool> EnviarERegistrarAsync(Email email, LoteEnvio lote, DateTimeOffset inicio, CancellationToken ct)
        {
            try
            {
                var resultado = await _sender.EnviarAsync(email, lote, ct);

                if (resultado.Sucesso)
                {
                    email.RegistrarSucesso(inicio);
                    return true;
                }

                email.RegistrarFalha(inicio, NormalizarCodigo(resultado.CodigoErro));
                return false;
            }
            catch (FalhaTemporariaEnvioException)
            {
                email.RegistrarFalha(inicio, CodigoSmtpIndisponivel);
                return false;
            }
            catch (Exception ex) when (ex is not OperationCanceledException && !EhFalhaDeBanco(ex))
            {
                _logger.LogError(ex, "Erro inesperado ao enviar o e-mail {EmailId}.", email.Id);
                email.RegistrarFalha(inicio, CodigoErroInesperado);
                return false;
            }
        }

        private static bool EhFalhaDeBanco(Exception ex)
        {
            for (var atual = ex; atual is not null; atual = atual.InnerException)
            {
                if (atual is System.Data.Common.DbException)
                    return true;
            }

            return false;
        }

        private static string NormalizarCodigo(string? codigo)
        {
            if (string.IsNullOrWhiteSpace(codigo))
                return CodigoErroInesperado;

            codigo = codigo.Trim();
            return codigo.Length <= CodigoErroMaxLength ? codigo : codigo[..CodigoErroMaxLength];
        }
    }
}
