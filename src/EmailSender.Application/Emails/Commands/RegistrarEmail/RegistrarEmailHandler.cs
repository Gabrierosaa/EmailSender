using System.Text.Json;
using EmailSender.Application.DTOs;
using EmailSender.Domain.Entities;
using EmailSender.Domain.Repositories;
using FluentValidation;

namespace EmailSender.Application.Emails.Commands.RegistrarEmail
{
    public sealed class RegistrarEmailHandler
    {
        private readonly ILoteEnvioRepository _lotes;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IValidator<CriarEmailRequest> _validator;

        public RegistrarEmailHandler(
            ILoteEnvioRepository lotes,
            IUnitOfWork unitOfWork,
            IValidator<CriarEmailRequest> validator)
        {
            _lotes = lotes;
            _unitOfWork = unitOfWork;
            _validator = validator;
        }

        public async Task<CriarEmailResponse> HandleAsync(CriarEmailRequest request, CancellationToken ct)
        {
            await _validator.ValidateAndThrowAsync(request, ct);

            var variaveisJson = request.Variaveis is { Count: > 0 }
                ? JsonSerializer.Serialize(request.Variaveis)
                : null;

            var lote = LoteEnvio.CriarUnitario(
                request.Assunto,
                request.RemetenteNome,
                request.Destinatario,
                nomeDestinatario: request.NomeDestinatario,
                variavelJson: variaveisJson,
                replyTo: string.IsNullOrWhiteSpace(request.ReplyTo) ? null : request.ReplyTo.Trim(),
                corpoHtml: request.CorpoHtml,
                corpoTexto: request.CorpoTexto);

            await _lotes.AdicionarAsync(lote, ct);
            await _unitOfWork.CommitAsync(ct);

            return new CriarEmailResponse(lote.Emails.Single().Id);
        }
    }
}
