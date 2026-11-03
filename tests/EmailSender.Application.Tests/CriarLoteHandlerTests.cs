using EmailSender.Application.DTOs;
using EmailSender.Application.Emails.Commands.CriarLote;
using EmailSender.Domain.Entities;
using EmailSender.Domain.Enum;
using EmailSender.Domain.Repositories;
using FluentValidation;
using NSubstitute;

namespace EmailSender.Application.Tests
{
    public class CriarLoteHandlerTests
    {
        private readonly ILoteEnvioRepository _lotes = Substitute.For<ILoteEnvioRepository>();
        private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
        private readonly CriarLoteHandler _handler;

        public CriarLoteHandlerTests()
        {
            _handler = new CriarLoteHandler(_lotes, _unitOfWork, new CriarLoteRequestValidator());
        }

        private static CriarLoteRequest Valido() =>
            new("Assunto", "Remetente", "r@teste.com", "<p>oi</p>", "oi", new List<DestinatarioLoteRequest>
            {
                new("a@teste.com", "Ana", new Dictionary<string, string> { ["codigo"] = "A1" }),
                new("b@teste.com"),
                new("c@teste.com", "Carla")
            });

        [Fact]
        public async Task Handle_Valido_DevePersistirTodosPendentesComUmUnicoCommit()
        {
            LoteEnvio? salvo = null;
            await _lotes.AdicionarAsync(Arg.Do<LoteEnvio>(l => salvo = l), Arg.Any<CancellationToken>());

            var resposta = await _handler.HandleAsync(Valido(), CancellationToken.None);

            Assert.NotNull(salvo);
            Assert.Equal(3, salvo!.Emails.Count);
            Assert.All(salvo.Emails, e => Assert.Equal(EmailStatusEnum.Pendente, e.Status));
            Assert.Equal("{\"codigo\":\"A1\"}", salvo.Emails.First().VariavelJson);
            Assert.Equal("{}", salvo.Emails.Last().VariavelJson);
            Assert.Equal("r@teste.com", salvo.ReplyTo);
            Assert.Equal(new CriarLoteResponse(salvo.Id, 3, "Processando"), resposta);
            await _lotes.Received(1).AdicionarAsync(Arg.Any<LoteEnvio>(), Arg.Any<CancellationToken>());
            await _unitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());
        }

        public static TheoryData<CriarLoteRequest> RequisicoesInvalidas()
        {
            var ok = Valido();
            return new TheoryData<CriarLoteRequest>
            {
                ok with { Assunto = "" },
                ok with { Assunto = new string('a', 301) },
                ok with { RemetenteNome = "" },
                ok with { RemetenteNome = new string('a', 201) },
                ok with { CorpoHtml = null, CorpoTexto = null },
                ok with { Destinatarios = null },
                ok with { Destinatarios = new List<DestinatarioLoteRequest>() },
                ok with { Destinatarios = new List<DestinatarioLoteRequest> { new("") } },
                ok with { Destinatarios = new List<DestinatarioLoteRequest> { new(new string('a', 250) + "@x.com") } },
                ok with { Destinatarios = new List<DestinatarioLoteRequest> { new("a@teste.com"), new("A@teste.com") } },
                ok with { Destinatarios = Enumerable.Range(0, 1001).Select(i => new DestinatarioLoteRequest($"u{i}@teste.com")).ToList() },
            };
        }

        [Theory]
        [MemberData(nameof(RequisicoesInvalidas))]
        public async Task Handle_EntradaInvalida_DeveLancarValidationException_NaoDomainException(CriarLoteRequest request)
        {
            await Assert.ThrowsAsync<ValidationException>(() => _handler.HandleAsync(request, CancellationToken.None));

            await _lotes.DidNotReceive().AdicionarAsync(Arg.Any<LoteEnvio>(), Arg.Any<CancellationToken>());
            await _unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_Valido_DevePersistirConteudoDoLote()
        {
            LoteEnvio? salvo = null;
            await _lotes.AdicionarAsync(Arg.Do<LoteEnvio>(l => salvo = l), Arg.Any<CancellationToken>());

            await _handler.HandleAsync(Valido(), CancellationToken.None);

            Assert.Equal("Assunto", salvo!.Assunto);
            Assert.Equal("Remetente", salvo.RemetenteNome);
            Assert.Equal("<p>oi</p>", salvo.CorpoHtml);
            Assert.Equal("oi", salvo.CorpoTexto);
            Assert.Equal(new[] { "a@teste.com", "b@teste.com", "c@teste.com" }, salvo.Emails.Select(e => e.Destinatario));
            Assert.Equal("Ana", salvo.Emails.First().NomeDestinatario);
        }

        [Fact]
        public async Task Handle_Valido_DeveRetornarTotalEStatusProcessando()
        {
            var resposta = await _handler.HandleAsync(Valido(), CancellationToken.None);

            Assert.Equal(3, resposta.TotalDestinatarios);
            Assert.Equal("Processando", resposta.Status);
        }

        [Fact]
        public async Task Handle_Cancelado_DevePropagar()
        {
            _unitOfWork.CommitAsync(Arg.Any<CancellationToken>())
                .Returns<int>(_ => throw new OperationCanceledException());

            await Assert.ThrowsAsync<OperationCanceledException>(
                () => _handler.HandleAsync(Valido(), new CancellationToken(true)));
        }

        [Fact]
        public void Handler_NaoDeveDependerDePortaDeEnvio()
        {
            var tipos = typeof(CriarLoteHandler).GetConstructors()
                .SelectMany(c => c.GetParameters())
                .Select(p => p.ParameterType)
                .ToArray();

            Assert.DoesNotContain(typeof(EmailSender.Application.Abstractions.IEmailSender), tipos);
        }

        [Fact]
        public async Task Handle_Invalido_DeveLancarValidationExceptionENaoPersistir()
        {
            var request = Valido() with { Destinatarios = new List<DestinatarioLoteRequest> { new("a@teste.com"), new("invalido") } };

            var excecao = await Assert.ThrowsAsync<ValidationException>(
                () => _handler.HandleAsync(request, CancellationToken.None));

            Assert.Contains(excecao.Errors, e => e.PropertyName == "Destinatarios[1].Email");
            await _lotes.DidNotReceive().AdicionarAsync(Arg.Any<LoteEnvio>(), Arg.Any<CancellationToken>());
            await _unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
        }
    }
}
