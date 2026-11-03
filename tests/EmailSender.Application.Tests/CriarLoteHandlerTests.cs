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
