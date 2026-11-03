using EmailSender.Application.Abstractions;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace EmailSender.Application.Tests
{
    public class ArquiteturaTests
    {
        [Fact]
        public void Application_NaoDeveReferenciarEntityFrameworkCoreNemInfrastructure()
        {
            var referencias = typeof(IEmailSender).Assembly.GetReferencedAssemblies();

            Assert.DoesNotContain(referencias, a => a.Name!.StartsWith("Microsoft.EntityFrameworkCore"));
            Assert.DoesNotContain(referencias, a => a.Name == "EmailSender.Infrastructure");
        }

        [Fact]
        public void TodoValidadorPublico_DeveEstarRegistradoEmAddApplication()
        {
            var provider = new ServiceCollection().AddApplication().BuildServiceProvider();

            var validadores = typeof(IEmailSender).Assembly.GetExportedTypes()
                .Where(t => !t.IsAbstract && t.BaseType is { IsGenericType: true } b
                    && b.GetGenericTypeDefinition() == typeof(AbstractValidator<>))
                .ToList();

            Assert.NotEmpty(validadores);
            Assert.All(validadores, validador =>
            {
                var alvo = validador.BaseType!.GetGenericArguments()[0];
                var servico = provider.GetService(typeof(IValidator<>).MakeGenericType(alvo));
                Assert.True(servico?.GetType() == validador,
                    $"{validador.Name} não está registrado em AddApplication.");
            });
        }

        [Fact]
        public void IEmailSender_DeveSerInterfacePublica()
        {
            Assert.True(typeof(IEmailSender).IsInterface && typeof(IEmailSender).IsPublic);
        }
    }
}
