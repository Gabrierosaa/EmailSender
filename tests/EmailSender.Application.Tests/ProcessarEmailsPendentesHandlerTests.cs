using EmailSender.Application.Abstractions;
using EmailSender.Application.Emails.Commands.ProcessarEmailsPendentes;
using EmailSender.Domain.Entities;
using EmailSender.Domain.Enum;
using EmailSender.Domain.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace EmailSender.Application.Tests
{
    public class ProcessarEmailsPendentesHandlerTests
    {
        private readonly IEmailRepository _emails = Substitute.For<IEmailRepository>();
        private readonly ILoteEnvioRepository _lotes = Substitute.For<ILoteEnvioRepository>();
        private readonly IEmailSender _sender = Substitute.For<IEmailSender>();
        private readonly LoteEnvio _lote = new("Assunto", "Remetente", corpoTexto: "Corpo");
        private readonly ProcessarEmailsPendentesHandler _handler;

        public ProcessarEmailsPendentesHandlerTests()
        {
            var services = new ServiceCollection();
            services.AddSingleton(_emails);
            services.AddSingleton(_lotes);
            services.AddSingleton(Substitute.For<IUnitOfWork>());
            services.AddSingleton(_sender);
            services.AddSingleton(TimeProvider.System);
            services.AddSingleton(PoliticaRetry.Padrao);
            services.AddScoped<ProcessarEmailHandler>();

            var provider = services.BuildServiceProvider();
            _handler = new ProcessarEmailsPendentesHandler(
                provider.GetRequiredService<IServiceScopeFactory>(),
                TimeProvider.System,
                NullLogger<ProcessarEmailsPendentesHandler>.Instance,
                new ProcessarEmailsPendentesCommandValidator());

            _lotes.ObterPorIdAsync(_lote.Id, Arg.Any<CancellationToken>()).Returns(_lote);
        }

        private List<Email> CriarPendentes(int quantidade)
        {
            var lista = Enumerable.Range(0, quantidade)
                .Select(i => new Email(_lote.Id, $"p{i}@teste.com", "Nome", "{}"))
                .ToList();

            foreach (var e in lista)
                _emails.ObterPorIdAsync(e.Id, Arg.Any<CancellationToken>()).Returns(e);

            _emails.ObterPendentesParaEnvioAsync(Arg.Any<DateTimeOffset>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns(ci => (IReadOnlyList<Email>)lista.Take(ci.ArgAt<int>(1)).ToList());

            return lista;
        }

        [Fact]
        public async Task EmailDoMeioFalha_DeveContinuarEResumir()
        {
            var emails = CriarPendentes(3);
            _sender.EnviarAsync(Arg.Any<Email>(), Arg.Any<LoteEnvio>(), Arg.Any<CancellationToken>())
                .Returns(ci => ci.Arg<Email>() == emails[1]
                    ? throw new InvalidOperationException("boom")
                    : new ResultadoEnvio(true, null));

            var resumo = await _handler.HandleAsync(new ProcessarEmailsPendentesCommand(), CancellationToken.None);

            Assert.Equal(new ResumoProcessamento(3, 2, 1, 0, 0), resumo);
            Assert.Equal(EmailStatusEnum.Enviado, emails[0].Status);
            Assert.Equal(EmailStatusEnum.Falhou, emails[1].Status);
            Assert.Equal(EmailStatusEnum.Enviado, emails[2].Status);
        }

        [Fact]
        public async Task Limite_DeveRepassarOLimiteParaORepositorio()
        {
            var emails = CriarPendentes(5);
            _sender.EnviarAsync(Arg.Any<Email>(), Arg.Any<LoteEnvio>(), Arg.Any<CancellationToken>())
                .Returns(new ResultadoEnvio(true, null));

            var resumo = await _handler.HandleAsync(new ProcessarEmailsPendentesCommand(Limite: 2), CancellationToken.None);

            Assert.Equal(2, resumo.Processados);
            Assert.Equal(2, resumo.Enviados);
            Assert.Equal(EmailStatusEnum.Pendente, emails[2].Status);
            await _emails.Received(1).ObterPendentesParaEnvioAsync(Arg.Any<DateTimeOffset>(), 2, Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task ErroInesperadoFora_DeveContarComoFalhoESeguir()
        {
            var emails = CriarPendentes(2);
            _emails.ObterPorIdAsync(emails[0].Id, Arg.Any<CancellationToken>())
                .Returns<Email?>(_ => throw new InvalidOperationException("inesperado"));
            _sender.EnviarAsync(Arg.Any<Email>(), Arg.Any<LoteEnvio>(), Arg.Any<CancellationToken>())
                .Returns(new ResultadoEnvio(true, null));

            var resumo = await _handler.HandleAsync(new ProcessarEmailsPendentesCommand(), CancellationToken.None);

            Assert.Equal(new ResumoProcessamento(2, 1, 1, 0, 0), resumo);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(1001)]
        public async Task LimiteInvalido_DeveLancarValidationException_SemConsultarORepositorio(int limite)
        {
            await Assert.ThrowsAsync<FluentValidation.ValidationException>(() =>
                _handler.HandleAsync(new ProcessarEmailsPendentesCommand(limite), CancellationToken.None));

            await _emails.DidNotReceiveWithAnyArgs().ObterPendentesParaEnvioAsync(default, default, default);
        }

        [Fact]
        public async Task CancelamentoDuranteOCiclo_DevePropagar()
        {
            CriarPendentes(2);
            using var cts = new CancellationTokenSource();
            _sender.EnviarAsync(Arg.Any<Email>(), Arg.Any<LoteEnvio>(), Arg.Any<CancellationToken>())
                .Returns(_ =>
                {
                    cts.Cancel();
                    return new ResultadoEnvio(true, null);
                });

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                _handler.HandleAsync(new ProcessarEmailsPendentesCommand(), cts.Token));
        }

        [Fact]
        public async Task ConflitoDeConcorrencia_DeveContarEmIgnorados()
        {
            var emails = CriarPendentes(2);
            var uow = Substitute.For<IUnitOfWork>();
            uow.CommitAsync(Arg.Any<CancellationToken>())
                .Returns<int>(_ => throw new EmailSender.Domain.Exceptions.ConflitoConcorrenciaException("conflito"));
            var services = new ServiceCollection();
            services.AddSingleton(_emails);
            services.AddSingleton(_lotes);
            services.AddSingleton(uow);
            services.AddSingleton(_sender);
            services.AddSingleton(TimeProvider.System);
            services.AddSingleton(PoliticaRetry.Padrao);
            services.AddScoped<ProcessarEmailHandler>();
            var handler = new ProcessarEmailsPendentesHandler(
                services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(),
                TimeProvider.System,
                NullLogger<ProcessarEmailsPendentesHandler>.Instance,
                new ProcessarEmailsPendentesCommandValidator());

            var resumo = await handler.HandleAsync(new ProcessarEmailsPendentesCommand(), CancellationToken.None);

            Assert.Equal(new ResumoProcessamento(2, 0, 0, 0, 2), resumo);
            await _sender.DidNotReceiveWithAnyArgs().EnviarAsync(default!, default!, default);
        }

        [Fact]
        public async Task FalhaTemporaria_DeveContarEmReagendados()
        {
            CriarPendentes(1);
            _sender.EnviarAsync(Arg.Any<Email>(), Arg.Any<LoteEnvio>(), Arg.Any<CancellationToken>())
                .Returns<ResultadoEnvio>(_ => throw new FalhaTemporariaEnvioException("timeout"));

            var resumo = await _handler.HandleAsync(new ProcessarEmailsPendentesCommand(), CancellationToken.None);

            Assert.Equal(new ResumoProcessamento(1, 0, 0, 1, 0), resumo);
        }

        [Fact]
        public async Task CadaEmail_DeveSerProcessadoEmUmEscopoNovo()
        {
            var emails = CriarPendentes(3);
            _sender.EnviarAsync(Arg.Any<Email>(), Arg.Any<LoteEnvio>(), Arg.Any<CancellationToken>())
                .Returns(new ResultadoEnvio(true, null));
            var escopos = new List<IServiceScope>();
            var factory = Substitute.For<IServiceScopeFactory>();
            var real = new ServiceCollection()
                .AddSingleton(_emails).AddSingleton(_lotes).AddSingleton(Substitute.For<IUnitOfWork>())
                .AddSingleton(_sender).AddSingleton(TimeProvider.System).AddSingleton(PoliticaRetry.Padrao)
                .AddScoped<ProcessarEmailHandler>()
                .BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
            factory.CreateScope().Returns(_ =>
            {
                var s = real.CreateScope();
                escopos.Add(s);
                return s;
            });
            var handler = new ProcessarEmailsPendentesHandler(
                factory, TimeProvider.System,
                NullLogger<ProcessarEmailsPendentesHandler>.Instance,
                new ProcessarEmailsPendentesCommandValidator());

            await handler.HandleAsync(new ProcessarEmailsPendentesCommand(), CancellationToken.None);

            Assert.Equal(emails.Count + 1, escopos.Count);
        }

        [Fact]
        public async Task SemPendentes_DeveDevolverResumoZerado()
        {
            CriarPendentes(0);

            var resumo = await _handler.HandleAsync(new ProcessarEmailsPendentesCommand(), CancellationToken.None);

            Assert.Equal(new ResumoProcessamento(0, 0, 0, 0, 0), resumo);
        }
    }
}
