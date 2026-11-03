namespace EmailSender.Application.Emails.Commands.ProcessarEmailsPendentes
{
    public sealed record ProcessarEmailsPendentesCommand(int Limite = 50);

    public sealed record ResumoProcessamento(int Processados, int Enviados, int Falhos, int Ignorados);
}
