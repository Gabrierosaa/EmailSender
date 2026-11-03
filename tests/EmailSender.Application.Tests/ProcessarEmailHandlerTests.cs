using EmailSender.Application.Abstractions;
using EmailSender.Application.Emails.Commands.ProcessarEmailsPendentes;
using EmailSender.Domain.Entities;
using EmailSender.Domain.Enum;
using EmailSender.Domain.Exceptions;
using EmailSender.Domain.Repositories;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace EmailSender.Application.Tests
{
    public class ProcessarEmailHandlerTests
    {
        private readonly IEmailRepository _emails = Substitute.For<IEmailRepository>();
        private readonly ILoteEnvioRepository _lotes = Substitute.For<ILoteEnvioRepository>();
        private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
        private readonly IEmailSender _sender = Substitute.For<IEmailSender>();
        private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 11, 3, 12, 0, 0, TimeSpan.Zero));
        private readonly ProcessarEmailHandler _handler;
        private readonly LoteEnvio _lote = new("Assunto", "Remetente", corpoTexto: "Corpo");
        private readonly Email _email;

        public ProcessarEmailHandlerTests()
        {
            _handler = CriarHandler(PoliticaRetry.Padrao);
            _email =new Email(_lote.Id, "a@teste.com", "Fulano", "{}");

            _emails.ObterPorIdAsync(_email.Id, Arg.Any<CancellationToken>()).Returns(_email);
            _lotes.ObterPorIdAsync(_lote.Id, Arg.Any<CancellationToken>()).Returns(_lote);
        }

        private ProcessarEmailHandler CriarHandler(PoliticaRetry retry) =>
            new(_emails, _lotes, _unitOfWork, _sender, _clock, retry);

        private void SenderLancaFalhaTemporaria() =>
            _sender.EnviarAsync(_email, _lote, Arg.Any<CancellationToken>())
                .Returns<ResultadoEnvio>(_ => throw new FalhaTemporariaEnvioException("smtp fora"));

        [Fact]
        public async Task Sucesso_DeveMarcarEnviadoERegistrarTentativaComSucesso()
        {
            _sender.EnviarAsync(_email, _lote, Arg.Any<CancellationToken>())
                .Returns(new ResultadoEnvio(true, null));

            var resultado = await _handler.HandleAsync(_email.Id, CancellationToken.None);

            Assert.Equal(ResultadoProcessamento.Enviado, resultado);
            Assert.Equal(EmailStatusEnum.Enviado, _email.Status);
            var tentativa = Assert.Single(_email.Tentativas);
            Assert.True(tentativa.Sucesso);
            Assert.Equal(_clock.GetUtcNow(), tentativa.IniciadaEm);
            await _unitOfWork.Received(2).CommitAsync(Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task FalhaPermanente_DeveMarcarFalhouComOCodigoRecebido()
        {
            _sender.EnviarAsync(_email, _lote, Arg.Any<CancellationToken>())
                .Returns(new ResultadoEnvio(false, "DESTINATARIO_REJEITADO"));

            var resultado = await _handler.HandleAsync(_email.Id, CancellationToken.None);

            Assert.Equal(ResultadoProcessamento.Falhou, resultado);
            Assert.Equal(EmailStatusEnum.Falhou, _email.Status);
            Assert.Equal("DESTINATARIO_REJEITADO", Assert.Single(_email.Tentativas).CodigoErro);
        }

        [Fact]
        public async Task FalhaSemCodigo_DeveUsarErroInesperado()
        {
            _sender.EnviarAsync(_email, _lote, Arg.Any<CancellationToken>())
                .Returns(new ResultadoEnvio(false, null));

            await _handler.HandleAsync(_email.Id, CancellationToken.None);

            Assert.Equal("ERRO_INESPERADO", Assert.Single(_email.Tentativas).CodigoErro);
        }

        [Fact]
        public async Task CodigoMuitoLongo_DeveSerTruncadoEm100Caracteres()
        {
            _sender.EnviarAsync(_email, _lote, Arg.Any<CancellationToken>())
                .Returns(new ResultadoEnvio(false, new string('X', 250)));

            await _handler.HandleAsync(_email.Id, CancellationToken.None);

            Assert.Equal(100, Assert.Single(_email.Tentativas).CodigoErro.Length);
        }

        [Fact]
        public async Task FalhaPermanente_NaoDeveAgendarNovaTentativa()
        {
            _sender.EnviarAsync(_email, _lote, Arg.Any<CancellationToken>())
                .Returns(new ResultadoEnvio(false, "SMTP_550"));

            await _handler.HandleAsync(_email.Id, CancellationToken.None);

            Assert.Equal(EmailStatusEnum.Falhou, _email.Status);
            Assert.Null(_email.ProximaTentativaEm);
            Assert.Single(_email.Tentativas);
        }

        [Fact]
        public async Task FalhaTemporariaComTentativasRestantes_DeveReagendarEm15Segundos()
        {
            SenderLancaFalhaTemporaria();

            var resultado = await _handler.HandleAsync(_email.Id, CancellationToken.None);

            Assert.Equal(ResultadoProcessamento.Reagendado, resultado);
            Assert.Equal(EmailStatusEnum.Pendente, _email.Status);
            Assert.Equal(_clock.GetUtcNow().AddSeconds(15), _email.ProximaTentativaEm);
            Assert.Equal("SMTP_INDISPONIVEL", Assert.Single(_email.Tentativas).CodigoErro);
        }

        [Fact]
        public async Task FalhaLenta_DeveContarOIntervaloAPartirDoFimDaFalha()
        {
            _sender.EnviarAsync(_email, _lote, Arg.Any<CancellationToken>())
                .Returns<ResultadoEnvio>(_ =>
                {
                    _clock.Advance(TimeSpan.FromSeconds(30)); // timeout longo
                    throw new FalhaTemporariaEnvioException("timeout");
                });

            await _handler.HandleAsync(_email.Id, CancellationToken.None);

            Assert.Equal(_clock.GetUtcNow().AddSeconds(15), _email.ProximaTentativaEm);
        }

        [Fact]
        public async Task SegundaFalhaTemporaria_DeveReagendarEm60Segundos()
        {
            SenderLancaFalhaTemporaria();
            await _handler.HandleAsync(_email.Id, CancellationToken.None);
            _clock.Advance(TimeSpan.FromSeconds(15));

            var resultado = await _handler.HandleAsync(_email.Id, CancellationToken.None);

            Assert.Equal(ResultadoProcessamento.Reagendado, resultado);
            Assert.Equal(EmailStatusEnum.Pendente, _email.Status);
            Assert.Equal(_clock.GetUtcNow().AddSeconds(60), _email.ProximaTentativaEm);
            Assert.Equal(2, _email.Tentativas.Count);
        }

        [Fact]
        public async Task TerceiraFalhaTemporaria_DeveEsgotarMarcandoFalhou()
        {
            SenderLancaFalhaTemporaria();
            await _handler.HandleAsync(_email.Id, CancellationToken.None);
            _clock.Advance(TimeSpan.FromSeconds(15));
            await _handler.HandleAsync(_email.Id, CancellationToken.None);
            _clock.Advance(TimeSpan.FromSeconds(60));

            var resultado = await _handler.HandleAsync(_email.Id, CancellationToken.None);

            Assert.Equal(ResultadoProcessamento.Falhou, resultado);
            Assert.Equal(EmailStatusEnum.Falhou, _email.Status);
            Assert.Null(_email.ProximaTentativaEm);
            Assert.Equal(3, _email.Tentativas.Count);
            Assert.Equal("SMTP_INDISPONIVEL", _email.Tentativas.Last().CodigoErro);
        }

        [Fact]
        public async Task SucessoNaSegundaTentativa_DeveMarcarEnviadoComDuasTentativas()
        {
            _sender.EnviarAsync(_email, _lote, Arg.Any<CancellationToken>())
                .Returns(
                    _ => throw new FalhaTemporariaEnvioException("smtp fora"),
                    _ => new ResultadoEnvio(true, null));

            await _handler.HandleAsync(_email.Id, CancellationToken.None);
            _clock.Advance(TimeSpan.FromSeconds(15));
            var resultado = await _handler.HandleAsync(_email.Id, CancellationToken.None);

            Assert.Equal(ResultadoProcessamento.Enviado, resultado);
            Assert.Equal(EmailStatusEnum.Enviado, _email.Status);
            Assert.Equal(2, _email.Tentativas.Count);
            Assert.False(_email.Tentativas.First().Sucesso);
            Assert.True(_email.Tentativas.Last().Sucesso);
        }

        [Fact]
        public async Task MaxTentativasIgualA1_NaoDeveReagendar()
        {
            var handler = CriarHandler(new PoliticaRetry(1, [TimeSpan.FromSeconds(15)]));
            SenderLancaFalhaTemporaria();

            var resultado = await handler.HandleAsync(_email.Id, CancellationToken.None);

            Assert.Equal(ResultadoProcessamento.Falhou, resultado);
            Assert.Equal(EmailStatusEnum.Falhou, _email.Status);
            Assert.Null(_email.ProximaTentativaEm);
        }

        [Fact]
        public async Task EmailComProximaTentativaFutura_DeveIgnorarSemEnviar()
        {
            SenderLancaFalhaTemporaria();
            await _handler.HandleAsync(_email.Id, CancellationToken.None);
            _sender.ClearReceivedCalls();

            var resultado = await _handler.HandleAsync(_email.Id, CancellationToken.None);

            Assert.Equal(ResultadoProcessamento.Ignorado, resultado);
            await _sender.DidNotReceiveWithAnyArgs().EnviarAsync(default!, default!, default);
        }

        [Fact]
        public async Task ExcecaoInesperada_DeveMarcarFalhouComErroInesperado()
        {
            _sender.EnviarAsync(_email, _lote, Arg.Any<CancellationToken>())
                .Returns<ResultadoEnvio>(_ => throw new InvalidOperationException("boom"));

            var resultado = await _handler.HandleAsync(_email.Id, CancellationToken.None);

            Assert.Equal(ResultadoProcessamento.Falhou, resultado);
            Assert.Equal(EmailStatusEnum.Falhou, _email.Status);
            Assert.Null(_email.ProximaTentativaEm);
            Assert.Equal("ERRO_INESPERADO", Assert.Single(_email.Tentativas).CodigoErro);
        }

        [Fact]
        public async Task Cancelamento_DevePropagarSemRegistrarTentativa()
        {
            _sender.EnviarAsync(_email, _lote, Arg.Any<CancellationToken>())
                .Returns<ResultadoEnvio>(_ => throw new OperationCanceledException());

            await Assert.ThrowsAsync<OperationCanceledException>(
                () => _handler.HandleAsync(_email.Id, CancellationToken.None));

            Assert.Empty(_email.Tentativas);
        }

        [Fact]
        public async Task EmailInexistente_DeveIgnorarSemEnviar()
        {
            var resultado = await _handler.HandleAsync(Guid.NewGuid(), CancellationToken.None);

            Assert.Equal(ResultadoProcessamento.Ignorado, resultado);
            await _sender.DidNotReceiveWithAnyArgs().EnviarAsync(default!, default!, default);
        }

        [Fact]
        public async Task EmailJaEnviado_DeveIgnorarSemEnviar()
        {
            _email.Enfileirar();
            _email.RegistrarSucesso(_clock.GetUtcNow());

            var resultado = await _handler.HandleAsync(_email.Id, CancellationToken.None);

            Assert.Equal(ResultadoProcessamento.Ignorado, resultado);
            await _sender.DidNotReceiveWithAnyArgs().EnviarAsync(default!, default!, default);
            await _unitOfWork.DidNotReceiveWithAnyArgs().CommitAsync(default);
        }

        [Fact]
        public async Task ConflitoNoPrimeiroCommit_DeveIgnorarSemEnviar()
        {
            _unitOfWork.CommitAsync(Arg.Any<CancellationToken>())
                .Returns<int>(_ => throw new ConflitoConcorrenciaException("conflito"));

            var resultado = await _handler.HandleAsync(_email.Id, CancellationToken.None);

            Assert.Equal(ResultadoProcessamento.Ignorado, resultado);
            await _sender.DidNotReceiveWithAnyArgs().EnviarAsync(default!, default!, default);
        }
    }
}
