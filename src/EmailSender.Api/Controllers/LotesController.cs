using EmailSender.Application.DTOs;
using EmailSender.Application.Emails.Commands.CriarLote;
using EmailSender.Application.Emails.Queries.ConsultarLote;
using Microsoft.AspNetCore.Mvc;

namespace EmailSender.Api.Controllers
{
    [ApiController]
    [Route("api/v1/lotes")]
    public class LotesController : ControllerBase
    {
        private readonly CriarLoteHandler _criarLote;
        private readonly ConsultarLoteHandler _consultarLote;

        public LotesController(CriarLoteHandler criarLote, ConsultarLoteHandler consultarLote)
        {
            _criarLote = criarLote;
            _consultarLote = consultarLote;
        }

        [HttpPost]
        [Consumes("application/json")]
        [ProducesResponseType<CriarLoteResponse>(StatusCodes.Status202Accepted, "application/json")]
        [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge, "application/problem+json")]
        public async Task<IActionResult> Criar([FromBody] CriarLoteRequest request, CancellationToken ct)
        {
            var resposta = await _criarLote.HandleAsync(request, ct);
            return Accepted($"/api/v1/lotes/{resposta.LoteId}", resposta);
        }

        [HttpGet("{id:guid}")]
        [ProducesResponseType<LoteStatusDto>(StatusCodes.Status200OK)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
        public async Task<IActionResult> Consultar(Guid id, CancellationToken ct)
        {
            var lote = await _consultarLote.HandleAsync(new ConsultarLoteQuery(id), ct);
            return lote is null ? NotFound() : Ok(lote);
        }
    }
}
