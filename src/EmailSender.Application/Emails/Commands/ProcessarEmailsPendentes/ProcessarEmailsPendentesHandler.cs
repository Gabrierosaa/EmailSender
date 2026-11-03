using System.Data.Common;
using EmailSender.Domain.Enum;
using EmailSender.Domain.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EmailSender.Application.Emails.Commands.ProcessarEmailsPendentes
{
    /// <summary>
    /// Processa um lote de e-mails Pendentes, um escopo (e um DbContext) por e-mail, para que um conflito
    /// ou erro em uma entidade não contamine o commit do próximo.
    /// </summary>
    public sealed class ProcessarEmailsPendentesHandler
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<ProcessarEmailsPendentesHandler> _logger;

        public ProcessarEmailsPendentesHandler(
            IServiceScopeFactory scopeFactory,
            ILogger<ProcessarEmailsPendentesHandler> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        public async Task<ResumoProcessamento> HandleAsync(ProcessarEmailsPendentesCommand command, CancellationToken ct)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(command.Limite, 1);

            var ids = await ObterIdsPendentesAsync(command.Limite, ct);

            int enviados = 0, falhos = 0, ignorados = 0;

            foreach (var id in ids)
            {
                ct.ThrowIfCancellationRequested();

                try
                {
                    await using var scope = _scopeFactory.CreateAsyncScope();
                    var handler = scope.ServiceProvider.GetRequiredService<ProcessarEmailHandler>();

                    switch (await handler.HandleAsync(id, ct))
                    {
                        case ResultadoProcessamento.Enviado: enviados++; break;
                        case ResultadoProcessamento.Falhou: falhos++; break;
                        default: ignorados++; break;
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException && !EhFalhaDeBanco(ex))
                {
                    falhos++;
                    _logger.LogError(ex, "Erro inesperado ao processar o e-mail {EmailId}.", id);
                }
            }

            return new ResumoProcessamento(ids.Count, enviados, falhos, ignorados);
        }

        private async Task<IReadOnlyList<Guid>> ObterIdsPendentesAsync(int limite, CancellationToken ct)
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var repositorio = scope.ServiceProvider.GetRequiredService<IEmailRepository>();

            var pendentes = await repositorio.ObterPorStatusAsync(EmailStatusEnum.Pendente, limite, ct);
            return pendentes.Select(e => e.Id).ToList();
        }

        // Banco indisponível afeta todos os e-mails: não isola, deixa propagar.
        private static bool EhFalhaDeBanco(Exception ex)
        {
            for (var atual = ex; atual is not null; atual = atual.InnerException)
            {
                if (atual is DbException)
                    return true;
            }

            return false;
        }
    }
}
