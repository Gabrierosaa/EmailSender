using EmailSender.Domain.Exceptions;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace EmailSender.Api.ErrorHandling
{
    public sealed class ApiExceptionHandler : IExceptionHandler
    {
        private readonly IProblemDetailsService _problemDetails;

        public ApiExceptionHandler(IProblemDetailsService problemDetails)
        {
            _problemDetails = problemDetails;
        }

        public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken ct)
        {
            ProblemDetails? problema = exception switch
            {
                ValidationException validacao => CriarProblemaDeValidacao(validacao),
                BadHttpRequestException { StatusCode: StatusCodes.Status413PayloadTooLarge } => new ProblemDetails
                {
                    Status = StatusCodes.Status413PayloadTooLarge,
                    Title = "O corpo da requisição é grande demais.",
                    Detail = $"O limite é de {Limites.CorpoRequisicaoBytes / (1024 * 1024)} MB."
                },
                BadHttpRequestException { StatusCode: StatusCodes.Status400BadRequest } => new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "Requisição inválida."
                },
                DomainException dominio => new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "Requisição inválida.",
                    Detail = dominio.Message
                },
                _ => null
            };

            if (problema is null)
                return false;

            httpContext.Response.StatusCode = problema.Status!.Value;

            return await _problemDetails.TryWriteAsync(new ProblemDetailsContext
            {
                HttpContext = httpContext,
                ProblemDetails = problema,
                Exception = exception
            });
        }

        private static ValidationProblemDetails CriarProblemaDeValidacao(ValidationException validacao)
        {
            var erros = validacao.Errors
                .GroupBy(e => NomeDoCampo(e.PropertyName))
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).Distinct().ToArray());

            return new ValidationProblemDetails(erros)
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Um ou mais campos são inválidos."
            };
        }

        private static string NomeDoCampo(string propriedade) =>
            string.Join('.', propriedade.Split('.').Select(segmento =>
                segmento.Length == 0 ? segmento : char.ToLowerInvariant(segmento[0]) + segmento[1..]));
    }
}
