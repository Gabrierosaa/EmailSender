using EmailSender.Application.DTOs;
using EmailSender.Domain.Enum;
using EmailSender.Domain.Repositories;

namespace EmailSender.Application.Emails.Queries.ConsultarLote
{
    public sealed class ConsultarLoteHandler
    {
        private readonly ILoteEnvioRepository _lotes;

        public ConsultarLoteHandler(ILoteEnvioRepository lotes) => _lotes = lotes;

        public async Task<LoteStatusDto?> HandleAsync(ConsultarLoteQuery query, CancellationToken ct)
        {
            var resumo = await _lotes.ObterResumoAsync(query.Id, ct);
            if (resumo is null) return null;

            int Contar(EmailStatusEnum status) => resumo.ContagemPorStatus.GetValueOrDefault(status);

            var pendentes = Contar(EmailStatusEnum.Pendente) + Contar(EmailStatusEnum.Enfileirado);
            var enviados = Contar(EmailStatusEnum.Enviado);
            var falhos = Contar(EmailStatusEnum.Falhou);

            var status = pendentes > 0 ? "Processando"
                : falhos == 0 ? "Concluido"
                : "ConcluidoComFalhas";

            return new LoteStatusDto(resumo.Id, pendentes + enviados + falhos, pendentes, enviados, falhos, status, resumo.CriadoEm);
        }
    }
}
