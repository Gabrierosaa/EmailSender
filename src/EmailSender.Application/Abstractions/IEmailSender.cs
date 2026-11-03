using EmailSender.Domain.Entities;

namespace EmailSender.Application.Abstractions
{
    public interface IEmailSender
    {
        Task<ResultadoEnvio> EnviarAsync(Email email, LoteEnvio lote, CancellationToken ct);
    }

    public sealed record ResultadoEnvio(bool Sucesso, string? CodigoErro);
}
