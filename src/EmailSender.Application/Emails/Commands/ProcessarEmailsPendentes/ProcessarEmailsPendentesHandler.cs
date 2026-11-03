using System.Data.Common;
using EmailSender.Domain.Repositories;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EmailSender.Application.Emails.Commands.ProcessarEmailsPendentes
{
    public sealed class ProcessarEmailsPendentesHandler : IProcessadorEmailsPendentes
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly TimeProvider _clock;
        private readonly ILogger<ProcessarEmailsPendentesHandler> _logger;
        private readonly IValidator<ProcessarEmailsPendentesCommand> _validator;

        public ProcessarEmailsPendentesHandler(
            IServiceScopeFactory scopeFactory,
            TimeProvider clock,
            ILogger<ProcessarEmailsPendentesHandler> logger,
            IValidator<ProcessarEmailsPendentesCommand> validator)
        {
            _validator = validator;
            _scopeFactory = scopeFactory;
            _clock = clock;
            _logger = logger;
        }

        public async Task<ResumoProcessamento> HandleAsync(ProcessarEmailsPendentesCommand command, CancellationToken ct)
        {
            await _validator.ValidateAndThrowAsync(command, ct);

            var ids = await ObterIdsPendentesAsync(command.Limite, ct);

            int enviados = 0, falhos = 0, reagendados = 0, ignorados = 0;

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
                        case ResultadoProcessamento.Reagendado: reagendados++; break;
                        default: ignorados++; break;
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException && !EhFalhaDeBanco(ex))
                {
                    falhos++;
                    _logger.LogError(ex, "Erro inesperado ao processar o e-mail {EmailId}.", id);
                }
            }

            return new ResumoProcessamento(ids.Count, enviados, falhos, reagendados, ignorados);
        }

        private async Task<IReadOnlyList<Guid>> ObterIdsPendentesAsync(int limite, CancellationToken ct)
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var repositorio = scope.ServiceProvider.GetRequiredService<IEmailRepository>();

            var pendentes = await repositorio.ObterPendentesParaEnvioAsync(_clock.GetUtcNow(), limite, ct);
            return pendentes.Select(e => e.Id).ToList();
        }

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
