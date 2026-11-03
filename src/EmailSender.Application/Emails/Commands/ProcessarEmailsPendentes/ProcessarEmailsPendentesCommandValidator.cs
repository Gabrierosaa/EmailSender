using FluentValidation;

namespace EmailSender.Application.Emails.Commands.ProcessarEmailsPendentes
{
    public sealed class ProcessarEmailsPendentesCommandValidator : AbstractValidator<ProcessarEmailsPendentesCommand>
    {
        public const int LimiteMaximo = 1000;

        public ProcessarEmailsPendentesCommandValidator()
        {
            RuleFor(c => c.Limite)
                .InclusiveBetween(1, LimiteMaximo)
                .WithMessage($"O limite deve estar entre 1 e {LimiteMaximo}.");
        }
    }
}
