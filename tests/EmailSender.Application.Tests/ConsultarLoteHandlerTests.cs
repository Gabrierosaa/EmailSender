using EmailSender.Application.Emails.Queries.ConsultarLote;
using EmailSender.Domain.Enum;
using EmailSender.Domain.Repositories;
using NSubstitute;

namespace EmailSender.Application.Tests
{
    public class ConsultarLoteHandlerTests
    {
        private readonly ILoteEnvioRepository _lotes = Substitute.For<ILoteEnvioRepository>();
        private readonly ConsultarLoteHandler _handler;
        private readonly Guid _id = Guid.NewGuid();
        private readonly DateTimeOffset _criadoEm = new(2026, 11, 3, 12, 0, 0, TimeSpan.Zero);

        public ConsultarLoteHandlerTests() => _handler = new ConsultarLoteHandler(_lotes);

        private void Resumo(params (EmailStatusEnum Status, int Total)[] contagens) =>
            _lotes.ObterResumoAsync(_id, Arg.Any<CancellationToken>()).Returns(
                new ResumoLote(_id, _criadoEm, contagens.ToDictionary(c => c.Status, c => c.Total)));

        [Fact]
        public async Task LoteInexistente_DeveRetornarNull()
        {
            _lotes.ObterResumoAsync(_id, Arg.Any<CancellationToken>()).Returns((ResumoLote?)null);

            Assert.Null(await _handler.HandleAsync(new ConsultarLoteQuery(_id), CancellationToken.None));
        }

        [Fact]
        public async Task ComPendentes_DeveEstarProcessando()
        {
            Resumo((EmailStatusEnum.Pendente, 1), (EmailStatusEnum.Enviado, 2));

            var dto = await _handler.HandleAsync(new ConsultarLoteQuery(_id), CancellationToken.None);

            Assert.Equal("Processando", dto!.Status);
            Assert.Equal((3, 1, 2, 0), (dto.TotalDestinatarios, dto.Pendentes, dto.Enviados, dto.Falhos));
            Assert.Equal(_id, dto.Id);
            Assert.Equal(_criadoEm, dto.CriadoEm);
        }

        [Fact]
        public async Task PendentesSomamEnfileirados()
        {
            Resumo((EmailStatusEnum.Pendente, 2), (EmailStatusEnum.Enfileirado, 3));

            var dto = await _handler.HandleAsync(new ConsultarLoteQuery(_id), CancellationToken.None);

            Assert.Equal(5, dto!.Pendentes);
            Assert.Equal("Processando", dto.Status);
        }

        [Fact]
        public async Task SoEnfileirados_DeveEstarProcessando()
        {
            Resumo((EmailStatusEnum.Enfileirado, 1), (EmailStatusEnum.Falhou, 1));

            var dto = await _handler.HandleAsync(new ConsultarLoteQuery(_id), CancellationToken.None);

            Assert.Equal("Processando", dto!.Status);
        }

        [Fact]
        public async Task TodosEnviados_DeveEstarConcluido()
        {
            Resumo((EmailStatusEnum.Enviado, 3));

            var dto = await _handler.HandleAsync(new ConsultarLoteQuery(_id), CancellationToken.None);

            Assert.Equal("Concluido", dto!.Status);
            Assert.Equal((3, 0, 3, 0), (dto.TotalDestinatarios, dto.Pendentes, dto.Enviados, dto.Falhos));
        }

        [Fact]
        public async Task SemPendentesComFalhas_DeveSerConcluidoComFalhas()
        {
            Resumo((EmailStatusEnum.Enviado, 2), (EmailStatusEnum.Falhou, 1));

            var dto = await _handler.HandleAsync(new ConsultarLoteQuery(_id), CancellationToken.None);

            Assert.Equal("ConcluidoComFalhas", dto!.Status);
            Assert.Equal(1, dto.Falhos);
        }
    }
}
