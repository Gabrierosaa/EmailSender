using EmailSender.Application.DTOs;
using EmailSender.Application.Emails.Commands.RegistrarEmail;
using EmailSender.Application.Emails.Queries.ConsultarStatus;
using Microsoft.AspNetCore.Mvc;

namespace EmailSender.Api.Controllers
{
    [ApiController]
    [Route("api/v1/emails")]
    public class EmailsController : ControllerBase
    {
        private readonly RegistrarEmailHandler _registrarEmail;
        private readonly ConsultarStatusHandler _consultarStatus;

        public EmailsController(RegistrarEmailHandler registrarEmail, ConsultarStatusHandler consultarStatus)
        {
            _registrarEmail = registrarEmail;
            _consultarStatus = consultarStatus;
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

        [HttpGet("{id:guid}/status")]
        [ProducesResponseType<EmailStatusDto>(StatusCodes.Status200OK)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
        public async Task<IActionResult> ConsultarStatus(Guid id, CancellationToken ct)
        {
            var status = await _consultarStatus.HandleAsync(new ConsultarStatusQuery(id), ct);
            return status is null ? NotFound() : Ok(status);
        }
    }
}
