using System.ComponentModel.DataAnnotations;

namespace EmailSender.Worker;

public sealed class EnvioEmailWorkerSettings
{
    public const string Secao = "Worker";

    [Range(1, 3600)] public int IntervaloSegundos { get; set; } = 10;
    [Range(1, 1000)] public int LimiteLote { get; set; } = 50;
}
