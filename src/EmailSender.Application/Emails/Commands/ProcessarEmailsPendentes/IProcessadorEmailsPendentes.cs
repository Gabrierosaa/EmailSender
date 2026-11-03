namespace EmailSender.Application.Emails.Commands.ProcessarEmailsPendentes
{
    public interface IProcessadorEmailsPendentes
    {
        Task<ResumoProcessamento> HandleAsync(ProcessarEmailsPendentesCommand command, CancellationToken ct);
    }
}
