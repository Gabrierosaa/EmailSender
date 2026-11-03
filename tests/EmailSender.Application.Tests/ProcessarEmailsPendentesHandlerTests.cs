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
                NullLogger<ProcessarEmailsPendentesHandler>.Instance);

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

        [Fact]
        public async Task SemPendentes_DeveDevolverResumoZerado()
        {
            CriarPendentes(0);

            var resumo = await _handler.HandleAsync(new ProcessarEmailsPendentesCommand(), CancellationToken.None);

            Assert.Equal(new ResumoProcessamento(0, 0, 0, 0, 0), resumo);
        }
    }
}
