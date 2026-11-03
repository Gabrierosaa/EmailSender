using System.Collections.Concurrent;
using EmailSender.Application.Abstractions;
using EmailSender.Domain.Entities;

namespace EmailSender.IntegrationTests
{
    internal sealed class FakeEmailSender : IEmailSender
    {
        private int _envios;

        public ConcurrentDictionary<string, ResultadoEnvio> Resultados { get; } = new();
        public ConcurrentDictionary<string, Exception> Excecoes { get; } = new();
        public ConcurrentDictionary<string, Queue<Func<ResultadoEnvio>>> Sequencia { get; } = new();
        public ConcurrentQueue<(Email Email, LoteEnvio Lote)> Chamadas { get; } = new();
        public TimeSpan Atraso { get; set; }
        public int Envios => _envios;

        public async Task<ResultadoEnvio> EnviarAsync(Email email, LoteEnvio lote, CancellationToken ct)
        {
            Interlocked.Increment(ref _envios);
            Chamadas.Enqueue((email, lote));
            if (Atraso > TimeSpan.Zero)
                await Task.Delay(Atraso, ct);

            if (Sequencia.TryGetValue(email.Destinatario, out var fila) && fila.Count > 0)
                return fila.Dequeue()();

            if (Excecoes.TryGetValue(email.Destinatario, out var excecao))
                throw excecao;

            return Resultados.TryGetValue(email.Destinatario, out var resultado)
                ? resultado
                : new ResultadoEnvio(true, null);
        }
    }
}
