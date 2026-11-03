using EmailSender.Application.DTOs;
using EmailSender.Application.Emails.Commands.RegistrarEmail;
using Microsoft.AspNetCore.Mvc;

namespace EmailSender.Api.Controllers
{
    [ApiController]
    [Route("api/v1/emails")]
    public class EmailsController : ControllerBase
    {
        private readonly RegistrarEmailHandler _registrarEmail;

        public EmailsController(RegistrarEmailHandler registrarEmail)
        {
            _registrarEmail = registrarEmail;
        }

        [HttpPost]
        [Consumes("application/json")]
        [ProducesResponseType<CriarEmailResponse>(StatusCodes.Status201Created, "application/json")]
        [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge, "application/problem+json")]
        public async Task<IActionResult> Criar([FromBody] CriarEmailRequest request, CancellationToken ct)
        {
            var resposta = await _registrarEmail.HandleAsync(request, ct);

            return Created($"/api/v1/emails/{resposta.Id}", resposta);
        }
    }
}
