using EmailSender.Application.Emails.Commands.ProcessarEmailsPendentes;
using Microsoft.Extensions.Options;

namespace EmailSender.Worker;

public sealed class EnvioEmailWorker(
    IProcessadorEmailsPendentes processador,
    IOptions<EnvioEmailWorkerSettings> options,
    ILogger<EnvioEmailWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var cfg = options.Value;
        var intervalo = TimeSpan.FromSeconds(cfg.IntervaloSegundos);
        logger.LogInformation("Worker iniciado. Intervalo: {Intervalo}s, lote: {Lote}.",
            cfg.IntervaloSegundos, cfg.LimiteLote);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var r = await processador.HandleAsync(
                    new ProcessarEmailsPendentesCommand(cfg.LimiteLote), stoppingToken);
                logger.LogInformation(
                    "Ciclo concluído. Processados: {P}, enviados: {E}, falhos: {F}, reagendados: {R}, ignorados: {I}.",
                    r.Processados, r.Enviados, r.Falhos, r.Reagendados, r.Ignorados);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Falha no ciclo de envio. Nova tentativa em {Intervalo}s.", cfg.IntervaloSegundos);
            }

            try { await Task.Delay(intervalo, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }

        logger.LogInformation("Worker encerrado.");
    }
}
