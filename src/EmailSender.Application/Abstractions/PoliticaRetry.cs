namespace EmailSender.Application.Abstractions
{
    public sealed class PoliticaRetry
    {
        public static PoliticaRetry Padrao { get; } =
            new(3, [TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(60)]);

        public int MaxTentativas { get; }
        public IReadOnlyList<TimeSpan> Intervalos { get; }

        public PoliticaRetry(int maxTentativas, IReadOnlyList<TimeSpan> intervalos)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(maxTentativas, 1);
            ArgumentNullException.ThrowIfNull(intervalos);
            if (intervalos.Count == 0)
                throw new ArgumentException("A lista de intervalos não pode ser vazia.", nameof(intervalos));

            MaxTentativas = maxTentativas;
            Intervalos = intervalos;
        }

        public bool PodeTentarNovamente(int tentativasFeitas) => tentativasFeitas < MaxTentativas;

        // Espera após a n-ésima falha (n >= 1). Se a lista for menor que o necessário, repete o último valor.
        public TimeSpan IntervaloApos(int tentativasFeitas)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(tentativasFeitas, 1);
            return Intervalos[Math.Min(tentativasFeitas, Intervalos.Count) - 1];
        }
    }
}
