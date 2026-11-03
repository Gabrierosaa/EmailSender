using EmailSender.Application.Emails.Commands.ProcessarEmailsPendentes;
using FluentValidation.TestHelper;

namespace EmailSender.Application.Tests
{
    public class ProcessarEmailsPendentesCommandValidatorTests
    {
        private readonly ProcessarEmailsPendentesCommandValidator _validator = new();

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(1001)]
        public void Limite_ForaDaFaixa_DeveFalharComMensagem(int limite)
        {
            var resultado = _validator.TestValidate(new ProcessarEmailsPendentesCommand(limite));

            resultado.ShouldHaveValidationErrorFor(c => c.Limite)
                .WithErrorMessage("O limite deve estar entre 1 e 1000.");
        }

        [Theory]
        [InlineData(1)]
        [InlineData(50)]
        [InlineData(1000)]
        public void Limite_NaFaixa_DeveSerValido(int limite)
        {
            _validator.TestValidate(new ProcessarEmailsPendentesCommand(limite))
                .ShouldNotHaveAnyValidationErrors();
        }

        [Fact]
        public void LimitePadrao_DeveSerValido()
        {
            _validator.TestValidate(new ProcessarEmailsPendentesCommand())
                .ShouldNotHaveAnyValidationErrors();
        }
    }
}
