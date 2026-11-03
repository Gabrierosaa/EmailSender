using System.Text.Json;
using EmailSender.Application.DTOs;
using EmailSender.Domain.Entities;
using EmailSender.Domain.Repositories;
using FluentValidation;

namespace EmailSender.Application.Emails.Commands.CriarLote
{
    /// <summary>
    /// Cria um lote com todos os destinatários (status Pendente) numa única transação. Só persiste: não envia por SMTP.
    /// </summary>
    public sealed class CriarLoteHandler
    {
        private readonly ILoteEnvioRepository _lotes;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IValidator<CriarLoteRequest> _validator;

        public CriarLoteHandler(
            ILoteEnvioRepository lotes,
            IUnitOfWork unitOfWork,
            IValidator<CriarLoteRequest> validator)
        {
            _lotes = lotes;
            _unitOfWork = unitOfWork;
            _validator = validator;
        }

        /// <exception cref="ValidationException">Quando a requisição é inválida; nada é salvo.</exception>
        public async Task<CriarLoteResponse> HandleAsync(CriarLoteRequest request, CancellationToken ct)
        {
            await _validator.ValidateAndThrowAsync(request, ct);

            var lote = new LoteEnvio(
                request.Assunto,
                request.RemetenteNome,
                replyTo: string.IsNullOrWhiteSpace(request.ReplyTo) ? null : request.ReplyTo.Trim(),
                corpoHtml: request.CorpoHtml,
                corpoTexto: request.CorpoTexto);

            foreach (var d in request.Destinatarios!)
            {
                var variaveisJson = d.Variaveis is { Count: > 0 }
                    ? JsonSerializer.Serialize(d.Variaveis)
                    : null;

                lote.AdicionarEmail(new Email(lote.Id, d.Email, d.Nome!, variaveisJson!));
            }

            await _lotes.AdicionarAsync(lote, ct);
            await _unitOfWork.CommitAsync(ct);

            return new CriarLoteResponse(lote.Id, lote.TotalDestinatarios, "Processando");
        }
    }
}
